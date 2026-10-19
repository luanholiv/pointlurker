using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Auth;
using PointLurker.Presentation.WebApi.Errors;
using IdentityError = Microsoft.AspNetCore.Identity.IdentityError;

namespace Presentation.WebApi.UnitTests.Errors;

public sealed class IdentityErrorMapperTests
{
    [Theory]
    [InlineData("PasswordTooShort", ErrorCodes.Validation.PasswordTooShort)]
    [InlineData("PasswordRequiresDigit", ErrorCodes.Validation.PasswordRequiresDigit)]
    [InlineData("PasswordRequiresUpper", ErrorCodes.Validation.PasswordRequiresUpper)]
    [InlineData("PasswordRequiresLower", ErrorCodes.Validation.PasswordRequiresLower)]
    [InlineData(
        "PasswordRequiresNonAlphanumeric",
        ErrorCodes.Validation.PasswordRequiresNonAlphanumeric
    )]
    [InlineData("PasswordRequiresUniqueChars", ErrorCodes.Validation.PasswordRequiresUniqueChars)]
    [InlineData("PasswordMismatch", ErrorCodes.Validation.PasswordMismatch)]
    [InlineData("DuplicateUserName", ErrorCodes.Validation.DuplicateUserName)]
    [InlineData("InvalidUserName", ErrorCodes.Validation.InvalidUserName)]
    [InlineData("DuplicateEmail", ErrorCodes.Validation.DuplicateEmail)]
    [InlineData("InvalidEmail", ErrorCodes.Validation.InvalidEmail)]
    public void Maps_identity_code_to_catalog_code_one_to_one(
        string identityCode,
        string expectedCatalogCode
    )
    {
        var error = new IdentityError { Code = identityCode, Description = "desc" };

        var mapped = IdentityErrorMapper.MapError(error);

        mapped.Code.Should().Be(expectedCatalogCode);
    }

    [Fact]
    public void PasswordTooShort_includes_min_param()
    {
        // ASP.NET Identity formats the description as "... at least X characters."
        var error = new IdentityError
        {
            Code = "PasswordTooShort",
            Description = "Passwords must be at least 8 characters.",
        };

        var mapped = IdentityErrorMapper.MapError(error);

        mapped.Code.Should().Be(ErrorCodes.Validation.PasswordTooShort);
        mapped.Params.Should().ContainKey("min");
        mapped.Params["min"].Should().Be(8);
    }

    [Fact]
    public void Unknown_identity_code_maps_to_validation_failed_with_field_code()
    {
        var error = new IdentityError
        {
            Code = "SomeUnknownCode",
            Description = "Something failed.",
        };

        var mapped = IdentityErrorMapper.MapError(error);

        // Unknown codes still produce a DomainError; caller decides how to wrap them
        mapped.Code.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// The single-user invariant is enforced as an Identity validator, so its failure arrives as an
    /// <c>IdentityError</c> like any other. Without an arm of its own it lands on the catch-all and
    /// renders "Invalid format." — the catalog already carries the right sentence for it.
    /// </summary>
    [Fact]
    public void SingleUserOnly_maps_to_the_register_already_exists_code()
    {
        var error = new IdentityError
        {
            Code = SingleUserValidator.SingleUserErrorCode,
            Description = "Only a single user account may exist.",
        };

        var mapped = IdentityErrorMapper.MapError(error);

        mapped.Code.Should().Be(ErrorCodes.Users.RegisterAlreadyExists);
    }

    [Fact]
    public void SingleUserOnly_is_reported_against_the_email_field()
    {
        var errors = new[]
        {
            new IdentityError
            {
                Code = SingleUserValidator.SingleUserErrorCode,
                Description = "Only a single user account may exist.",
            },
        };

        var pd = IdentityErrorMapper.ToValidationProblemDetails(errors);

        FirstEntry(pd)["field"].Should().Be("email");
    }

    /// <summary>
    /// Identity's own <c>ValidationProblem</c> keys its <c>errors</c> object by error code with the
    /// description as the single value — exactly the two fields of an <c>IdentityError</c>. This
    /// overload is what lets a response body be mapped back through the same table.
    /// </summary>
    [Fact]
    public void ToValidationProblemDetails_maps_an_identity_errors_dictionary()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["PasswordTooShort"] = ["Passwords must be at least 8 characters."],
        };

        var pd = IdentityErrorMapper.ToValidationProblemDetails(errors);

        pd.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);
        var entry = FirstEntry(pd);
        entry["code"].Should().Be(ErrorCodes.Validation.PasswordTooShort);
        entry["field"].Should().Be("password");
        var entryParams = entry["params"] as IReadOnlyDictionary<string, object?>;
        entryParams.Should().NotBeNull();
        entryParams!["min"].Should().Be(8);
    }

    [Fact]
    public void ToValidationProblemDetails_keeps_one_entry_per_message()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["PasswordRequiresDigit"] = ["Needs a digit."],
            ["PasswordRequiresUpper"] = ["Needs an uppercase letter."],
        };

        var pd = IdentityErrorMapper.ToValidationProblemDetails(errors);

        Entries(pd).Should().HaveCount(2);
    }

    [Fact]
    public void ToValidationProblemDetails_wraps_errors_in_validation_failed_shape()
    {
        var errors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Too short." },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "Needs digit." },
        };

        var pd = IdentityErrorMapper.ToValidationProblemDetails(errors);

        pd.Extensions.Should().ContainKey("code");
        pd.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);
        pd.Extensions.Should().ContainKey("params");
        var p = pd.Extensions["params"] as IReadOnlyDictionary<string, object?>;
        p.Should().ContainKey("errors");
    }

    /// <summary>
    /// The <c>manage/2fa</c> endpoint answers a failed enrolment with a <c>ValidationProblem</c>
    /// whose own code names which half failed, so both outcomes are distinguishable at the response.
    /// Without these arms they land on the catch-all and render "Invalid format." — the catalog
    /// already carries the right sentence for each.
    /// </summary>
    [Theory]
    [InlineData("InvalidTwoFactorCode", ErrorCodes.Auth.TwoFactorInvalid)]
    [InlineData("RequiresTwoFactor", ErrorCodes.Auth.TwoFactorRequired)]
    public void Maps_the_two_factor_enrolment_outcomes(
        string identityCode,
        string expectedCatalogCode
    )
    {
        var error = new IdentityError { Code = identityCode, Description = "desc" };

        var mapped = IdentityErrorMapper.MapError(error);

        mapped.Code.Should().Be(expectedCatalogCode);
    }

    [Theory]
    [InlineData("InvalidTwoFactorCode")]
    [InlineData("RequiresTwoFactor")]
    public void Two_factor_enrolment_failures_are_reported_against_the_code_field(
        string identityCode
    )
    {
        var errors = new[]
        {
            new IdentityError { Code = identityCode, Description = "desc" },
        };

        var pd = IdentityErrorMapper.ToValidationProblemDetails(errors);

        FirstEntry(pd)["field"].Should().Be("twoFactorCode");
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Entries(ProblemDetails pd)
    {
        var p = (IReadOnlyDictionary<string, object?>)pd.Extensions["params"]!;
        return ((IEnumerable<object?>)p["errors"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToList();
    }

    private static IReadOnlyDictionary<string, object?> FirstEntry(ProblemDetails pd) =>
        Entries(pd)[0];
}
