using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Presentation.WebApi.Validation;

/// <summary>
/// Projects a FluentValidation result into <see cref="FieldError"/> entries for
/// <see cref="FieldErrorProblem"/> to wrap. Nothing is recovered by parsing an English rule message:
/// the code travels on <see cref="ValidationFailure.ErrorCode"/> and the interpolation values on
/// <see cref="ValidationFailure.FormattedMessagePlaceholderValues"/>.
/// </summary>
internal static class ValidationFailureMapper
{
    /// <summary>
    /// Placeholder keys that may be forwarded into <c>params</c>, paired with the catalog param name
    /// they land under.
    /// </summary>
    /// <remarks>
    /// An allowlist, deliberately — not a denylist. FluentValidation also puts the rejected input in
    /// <c>PropertyValue</c>, and one validated contract carries a provider credential, so a
    /// denylist that ever missed a placeholder would put that secret in a response body and in the
    /// logs. A new rule that needs a new param adds it here explicitly. The allowlist governs this
    /// producer only: <c>SearchQuery</c> builds its own entries, and the query values it reports are
    /// the caller's own filters rather than anything secret.
    /// </remarks>
    private static readonly (string Placeholder, string Param)[] ForwardedPlaceholders =
    [
        ("MaxLength", "max"),
        ("MinLength", "min"),
        // The two bounds an inclusive-range rule declares. Both are constants written into the
        // rule, never the rejected input — that arrives under PropertyValue, which stays out.
        ("From", "min"),
        ("To", "max"),
    ];

    public static ProblemDetails ToProblemDetails(ValidationResult result) =>
        FieldErrorProblem.Create(result.Errors.Select(ToFieldError).ToArray());

    private static FieldError ToFieldError(ValidationFailure failure) =>
        new(
            ToCamelCase(failure.PropertyName),
            ToCatalogCode(failure.ErrorCode),
            ToEntryParams(failure)
        );

    /// <summary>
    /// FluentValidation defaults <see cref="ValidationFailure.ErrorCode"/> to the failing
    /// validator's type name, so a rule that forgot <c>WithErrorCode</c> would otherwise send
    /// <c>NotEmptyValidator</c> to a catalog that cannot resolve it. Anything not shaped like a
    /// declared code degrades to one that is.
    /// <c>Architecture.Tests</c> keeps this branch unreachable.
    /// </summary>
    private static string ToCatalogCode(string? errorCode) =>
        errorCode is not null && IsCodeShaped(errorCode)
            ? errorCode
            : ErrorCodes.Validation.InvalidFormat;

    private static bool IsCodeShaped(string code) =>
        code.Contains('.', StringComparison.Ordinal)
        && code.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_');

    private static IReadOnlyDictionary<string, object?> ToEntryParams(ValidationFailure failure)
    {
        var entryParams = new Dictionary<string, object?>();

        foreach (var (placeholder, param) in ForwardedPlaceholders)
        {
            if (
                failure.FormattedMessagePlaceholderValues?.TryGetValue(placeholder, out var value)
                == true
            )
            {
                entryParams[param] = value;
            }
        }

        return entryParams;
    }

    /// <summary>
    /// A collection rule reports <c>Values[0].LabelCanonical</c>. Each dot-separated segment is
    /// lower-cased at its first character, which leaves a trailing indexer intact so the client can
    /// point at the offending element.
    /// </summary>
    private static string ToCamelCase(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(LowerFirst));

    private static string LowerFirst(string segment) =>
        segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..];
}
