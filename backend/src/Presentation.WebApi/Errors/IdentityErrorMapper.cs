using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Auth;
using IdentityError = Microsoft.AspNetCore.Identity.IdentityError;

namespace PointLurker.Presentation.WebApi.Errors;

/// <summary>
/// Translates ASP.NET Identity error codes to domain catalog codes.
/// Maps one-to-one: each Identity error → one <see cref="DomainError"/>.
/// </summary>
public static class IdentityErrorMapper
{
    private static readonly Regex AtLeastNCharsPattern = new(
        @"at least (\d+) character",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>Maps a single Identity error to a <see cref="DomainError"/>.</summary>
    public static DomainError MapError(IdentityError error)
    {
        return error.Code switch
        {
            "PasswordTooShort" => new DomainError(
                ErrorCodes.Validation.PasswordTooShort,
                new Dictionary<string, object?> { ["min"] = ParseMin(error.Description) }
            ),
            "PasswordRequiresUniqueChars" => new DomainError(
                ErrorCodes.Validation.PasswordRequiresUniqueChars,
                new Dictionary<string, object?> { ["min"] = ParseMin(error.Description) }
            ),
            "PasswordRequiresDigit" => new DomainError(ErrorCodes.Validation.PasswordRequiresDigit),
            "PasswordRequiresUpper" => new DomainError(ErrorCodes.Validation.PasswordRequiresUpper),
            "PasswordRequiresLower" => new DomainError(ErrorCodes.Validation.PasswordRequiresLower),
            "PasswordRequiresNonAlphanumeric" => new DomainError(
                ErrorCodes.Validation.PasswordRequiresNonAlphanumeric
            ),
            "PasswordMismatch" => new DomainError(ErrorCodes.Validation.PasswordMismatch),
            "DuplicateUserName" => new DomainError(
                ErrorCodes.Validation.DuplicateUserName,
                new Dictionary<string, object?> { ["userName"] = ExtractQuoted(error.Description) }
            ),
            "InvalidUserName" => new DomainError(
                ErrorCodes.Validation.InvalidUserName,
                new Dictionary<string, object?> { ["userName"] = ExtractQuoted(error.Description) }
            ),
            "DuplicateEmail" => new DomainError(
                ErrorCodes.Validation.DuplicateEmail,
                new Dictionary<string, object?> { ["email"] = ExtractQuoted(error.Description) }
            ),
            "InvalidEmail" => new DomainError(
                ErrorCodes.Validation.InvalidEmail,
                new Dictionary<string, object?> { ["email"] = ExtractQuoted(error.Description) }
            ),
            // The enrolment endpoint names which half of the second factor failed, so these two
            // outcomes are distinguishable at the response — unlike a rejected code at sign-in.
            "InvalidTwoFactorCode" => new DomainError(ErrorCodes.Auth.TwoFactorInvalid),
            "RequiresTwoFactor" => new DomainError(ErrorCodes.Auth.TwoFactorRequired),
            SingleUserValidator.SingleUserErrorCode => new DomainError(
                ErrorCodes.Users.RegisterAlreadyExists
            ),
            _ => new DomainError(
                ErrorCodes.Validation.InvalidFormat,
                new Dictionary<string, object?>
                {
                    ["field"] = error.Code,
                    ["format"] = error.Description,
                }
            ),
        };
    }

    /// <summary>
    /// Wraps the <c>errors</c> object of a framework Identity
    /// <c>ValidationProblem</c> response: it is keyed by Identity error code, with the description
    /// as the value, so each pair reconstructs the <see cref="IdentityError"/> the mapping table
    /// expects. One entry is emitted per message, not per key.
    /// </summary>
    public static ProblemDetails ToValidationProblemDetails(
        IReadOnlyDictionary<string, string[]> errors
    )
    {
        ArgumentNullException.ThrowIfNull(errors);

        return ToValidationProblemDetails(
            errors.SelectMany(pair =>
                pair.Value.Select(description => new IdentityError
                {
                    Code = pair.Key,
                    Description = description,
                })
            )
        );
    }

    /// <summary>
    /// Wraps a collection of Identity errors into a <c>validation.failed</c> ProblemDetails body
    /// with <c>params.errors</c> containing per-error <c>{ field, code, params }</c> entries.
    /// </summary>
    public static ProblemDetails ToValidationProblemDetails(IEnumerable<IdentityError> errors)
    {
        var errorList = errors
            .Select(e =>
            {
                var mapped = MapError(e);
                return new Dictionary<string, object?>
                {
                    ["field"] = GetField(e.Code),
                    ["code"] = mapped.Code,
                    ["params"] = mapped.Params,
                };
            })
            .ToArray();

        var pd = new ProblemDetails
        {
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Type = $"https://errors.app/{ErrorCodes.Validation.Failed.Replace('.', '/')}",
        };

        pd.Extensions["code"] = ErrorCodes.Validation.Failed;
        pd.Extensions["params"] =
            (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?> { ["errors"] = errorList };

        return pd;
    }

    private static string GetField(string identityErrorCode) =>
        identityErrorCode switch
        {
            "PasswordTooShort"
            or "PasswordRequiresDigit"
            or "PasswordRequiresUpper"
            or "PasswordRequiresLower"
            or "PasswordRequiresNonAlphanumeric"
            or "PasswordRequiresUniqueChars"
            or "PasswordMismatch" => "password",
            "DuplicateUserName" or "InvalidUserName" => "userName",
            "DuplicateEmail" or "InvalidEmail" or SingleUserValidator.SingleUserErrorCode =>
                "email",
            "InvalidTwoFactorCode" or "RequiresTwoFactor" => "twoFactorCode",
            _ => "unknown",
        };

    private static object? ParseMin(string description)
    {
        var match = AtLeastNCharsPattern.Match(description);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var min))
            return min;
        return null;
    }

    private static string? ExtractQuoted(string description)
    {
        var start = description.IndexOf('\'', StringComparison.Ordinal);
        var end = description.IndexOf('\'', start + 1);
        if (start >= 0 && end > start)
            return description[(start + 1)..end];
        return null;
    }
}
