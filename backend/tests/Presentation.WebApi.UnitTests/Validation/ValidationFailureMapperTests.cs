using System.Text.Json;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Validation;

namespace Presentation.WebApi.UnitTests.Validation;

/// <summary>
/// The per-field contract every client renders from: a <c>validation.failed</c> wrapper whose
/// <c>params.errors</c> entries carry <c>{ field, code, params }</c> — the same shape
/// <c>IdentityErrorMapper</c> emits, so one frontend branch resolves both producers.
/// </summary>
public class ValidationFailureMapperTests
{
    [Fact]
    public void Wraps_failures_in_the_validation_failed_shape()
    {
        var pd = Map(Failure("DisplayName", ErrorCodes.Validation.Required));

        pd.Status.Should().Be(StatusCodes.Status400BadRequest);
        pd.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);
        pd.Type.Should().Be("https://errors.app/validation/failed");
    }

    [Fact]
    public void Keeps_one_entry_per_failure()
    {
        var pd = Map(
            Failure("ChannelId", ErrorCodes.Validation.Required),
            Failure("DisplayName", ErrorCodes.Validation.Required)
        );

        Entries(pd).Should().HaveCount(2);
    }

    [Fact]
    public void Carries_the_error_code_the_rule_named()
    {
        var pd = Map(Failure("RedemptionMessage", ErrorCodes.Validation.TooLong));

        FirstEntry(pd)["code"].Should().Be(ErrorCodes.Validation.TooLong);
    }

    /// <summary>
    /// A rule may name any declared code, not only a <c>validation.*</c> one — which is how
    /// <c>credentials.secret_required</c> keeps its specific sentence instead of degrading to the
    /// generic "this field is required".
    /// </summary>
    [Fact]
    public void Carries_a_code_from_another_namespace_unchanged()
    {
        var pd = Map(Failure("Secret", ErrorCodes.Credentials.SecretRequired));

        FirstEntry(pd)["code"].Should().Be(ErrorCodes.Credentials.SecretRequired);
    }

    /// <summary>
    /// FluentValidation defaults <c>ErrorCode</c> to the validator's own type name when a rule
    /// forgets <c>WithErrorCode</c>. That is not a code any catalog can resolve, so it degrades to
    /// a declared one rather than reaching the client as <c>NotEmptyValidator</c>.
    /// </summary>
    [Fact]
    public void Falls_back_to_invalid_format_when_the_rule_names_no_code_shaped_value()
    {
        var pd = Map(Failure("DisplayName", "NotEmptyValidator"));

        FirstEntry(pd)["code"].Should().Be(ErrorCodes.Validation.InvalidFormat);
    }

    [Fact]
    public void Falls_back_to_invalid_format_when_the_rule_names_no_code_at_all()
    {
        var pd = Map(Failure("DisplayName", errorCode: null));

        FirstEntry(pd)["code"].Should().Be(ErrorCodes.Validation.InvalidFormat);
    }

    [Theory]
    [InlineData("DisplayName", "displayName")]
    [InlineData("Token", "token")]
    [InlineData("StoreItemId", "storeItemId")]
    public void Camel_cases_the_field_name(string propertyName, string expected)
    {
        var pd = Map(Failure(propertyName, ErrorCodes.Validation.Required));

        FirstEntry(pd)["field"].Should().Be(expected);
    }

    /// <summary>
    /// A collection rule reports <c>Values[0].LabelCanonical</c>. Every segment is camel-cased and
    /// the indexer survives, so the client can point at the offending element.
    /// </summary>
    [Fact]
    public void Camel_cases_every_segment_of_a_nested_path_and_keeps_the_indexer()
    {
        var pd = Map(Failure("Values[0].LabelCanonical", ErrorCodes.Validation.Required));

        FirstEntry(pd)["field"].Should().Be("values[0].labelCanonical");
    }

    [Fact]
    public void Repeats_the_field_in_the_entry_params()
    {
        var pd = Map(Failure("DisplayName", ErrorCodes.Validation.Required));

        EntryParams(FirstEntry(pd))["field"].Should().Be("displayName");
    }

    [Fact]
    public void Exposes_the_max_length_from_the_rule_placeholders()
    {
        var failure = Failure(
            "RedemptionMessage",
            ErrorCodes.Validation.TooLong,
            placeholders: new Dictionary<string, object> { ["MaxLength"] = 1000 }
        );

        EntryParams(FirstEntry(Map(failure)))["max"].Should().Be(1000);
    }

    [Fact]
    public void Exposes_the_min_length_from_the_rule_placeholders()
    {
        var failure = Failure(
            "Locale",
            ErrorCodes.Validation.TooShort,
            placeholders: new Dictionary<string, object> { ["MinLength"] = 2 }
        );

        EntryParams(FirstEntry(Map(failure)))["min"].Should().Be(2);
    }

    /// <summary>
    /// The backend returns codes, never sentences — an English rule message in the body is the
    /// defect the MVC-bound mapper shipped.
    /// </summary>
    [Fact]
    public void Never_emits_a_message_key()
    {
        var pd = Map(Failure("DisplayName", ErrorCodes.Validation.Required));

        FirstEntry(pd).Should().NotContainKey("message");
    }

    /// <summary>
    /// FluentValidation puts the rejected value in <c>PropertyValue</c>, and one validated contract
    /// carries the StreamElements JWT. Params are an allowlist for that reason: a denylist that
    /// ever missed a placeholder would put the token in a response body and in the logs.
    /// </summary>
    [Fact]
    public void Never_copies_the_submitted_value_into_the_response()
    {
        const string secret = "eyJhbGciOiJIUzI1NiJ9.super-secret-se-token.signature";
        var failure = Failure(
            "Secret",
            ErrorCodes.Credentials.SecretRequired,
            placeholders: new Dictionary<string, object>
            {
                ["PropertyValue"] = secret,
                ["PropertyName"] = "Secret",
                ["MaxLength"] = 64,
            }
        );

        var pd = Map(failure);

        var json = JsonSerializer.Serialize(pd.Extensions);
        json.Should().NotContain(secret);
        EntryParams(FirstEntry(pd)).Keys.Should().BeEquivalentTo(["field", "max"]);
    }

    private static ProblemDetails Map(params ValidationFailure[] failures) =>
        ValidationFailureMapper.ToProblemDetails(new ValidationResult(failures));

    private static ValidationFailure Failure(
        string propertyName,
        string? errorCode,
        Dictionary<string, object>? placeholders = null
    )
    {
        var failure = new ValidationFailure(propertyName, "unused english message")
        {
            ErrorCode = errorCode!,
        };

        if (placeholders is not null)
        {
            failure.FormattedMessagePlaceholderValues = placeholders;
        }

        return failure;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Entries(ProblemDetails pd)
    {
        var wrapper = (IReadOnlyDictionary<string, object?>)pd.Extensions["params"]!;
        return ((IEnumerable<object?>)wrapper["errors"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToList();
    }

    private static IReadOnlyDictionary<string, object?> FirstEntry(ProblemDetails pd) =>
        Entries(pd)[0];

    private static IReadOnlyDictionary<string, object?> EntryParams(
        IReadOnlyDictionary<string, object?> entry
    ) => (IReadOnlyDictionary<string, object?>)entry["params"]!;
}
