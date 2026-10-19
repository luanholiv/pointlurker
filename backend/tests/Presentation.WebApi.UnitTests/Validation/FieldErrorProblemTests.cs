using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Validation;

namespace Presentation.WebApi.UnitTests.Validation;

/// <summary>
/// The one place the <c>validation.failed</c> envelope is built. Three producers project into it —
/// the FluentValidation filter for bodies, <c>SearchQuery</c> for query strings, and
/// <c>IdentityErrorMapper</c> for the framework Identity endpoints — so the entry shape
/// <c>{ field, code, params }</c> is settled here rather than three times over.
/// </summary>
public class FieldErrorProblemTests
{
    [Fact]
    public void Wraps_entries_in_the_validation_failed_shape()
    {
        var pd = FieldErrorProblem.Create([Error("displayName", ErrorCodes.Validation.Required)]);

        pd.Status.Should().Be(StatusCodes.Status400BadRequest);
        pd.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);
        pd.Type.Should().Be("https://errors.app/validation/failed");
    }

    [Fact]
    public void Keeps_one_entry_per_error_in_the_order_given()
    {
        var pd = FieldErrorProblem.Create([
            Error("sort", ErrorCodes.Search.InvalidEnumValue),
            Error("min", ErrorCodes.Search.InvalidFilterValue),
        ]);

        var entries = Entries(pd);
        entries.Should().HaveCount(2);
        entries[0]["field"].Should().Be("sort");
        entries[1]["field"].Should().Be("min");
    }

    [Fact]
    public void Carries_the_code_the_producer_named()
    {
        var pd = FieldErrorProblem.Create([Error("query", ErrorCodes.StoreLookup.QueryRequired)]);

        FirstEntry(pd)["code"].Should().Be(ErrorCodes.StoreLookup.QueryRequired);
    }

    /// <summary>
    /// Every entry repeats its field inside <c>params</c>, because that is what the catalog
    /// interpolates from. Guaranteeing it here is what keeps the three producers from each
    /// remembering to do it.
    /// </summary>
    [Fact]
    public void Repeats_the_field_in_the_entry_params()
    {
        var pd = FieldErrorProblem.Create([Error("username", ErrorCodes.Validation.Required)]);

        EntryParams(FirstEntry(pd))["field"].Should().Be("username");
    }

    [Fact]
    public void Forwards_the_params_the_producer_supplied()
    {
        var pd = FieldErrorProblem.Create([
            new FieldError(
                "sort",
                ErrorCodes.Search.InvalidEnumValue,
                new Dictionary<string, object?>
                {
                    ["parameter"] = "sort",
                    ["value"] = "bogus",
                    ["allowed"] = new[] { "Name", "Cost" },
                }
            ),
        ]);

        var entryParams = EntryParams(FirstEntry(pd));
        entryParams["parameter"].Should().Be("sort");
        entryParams["value"].Should().Be("bogus");
        entryParams["allowed"].Should().BeEquivalentTo(new[] { "Name", "Cost" });
        entryParams["field"].Should().Be("sort");
    }

    /// <summary>
    /// The backend answers with codes, never sentences: an English message in the body is what the
    /// frontend catalog exists to replace.
    /// </summary>
    [Fact]
    public void Never_emits_a_message_key()
    {
        var pd = FieldErrorProblem.Create([Error("username", ErrorCodes.Validation.Required)]);

        FirstEntry(pd).Should().NotContainKey("message");
    }

    [Fact]
    public void Names_the_generic_summary_as_the_developer_facing_title()
    {
        var pd = FieldErrorProblem.Create([Error("username", ErrorCodes.Validation.Required)]);

        pd.Title.Should().Be("One or more validation errors occurred.");
    }

    private static FieldError Error(string field, string code) => new(field, code);

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
