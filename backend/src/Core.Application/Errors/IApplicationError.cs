namespace PointLurker.Core.Application.Errors;

/// <summary>
/// A failure returned by a use case, carrying everything the HTTP layer needs to build an
/// RFC 7807 response: the canonical error code, the failure kind, an English developer-facing
/// title, and the interpolation params. Nothing is ever recovered by parsing the title.
/// </summary>
public interface IApplicationError
{
    /// <summary>
    /// Stable code from <see cref="Core.Domain.Errors.ErrorCodes"/> — never an inline literal.
    /// </summary>
    string Code { get; }

    /// <summary>Failure classification the presentation layer turns into an HTTP status.</summary>
    ErrorKind Kind { get; }

    /// <summary>
    /// English developer-facing message. Never localized — the frontend renders user-facing
    /// text from <see cref="Code"/> plus <see cref="Params"/>.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Placeholders for the frontend message catalog, or <see langword="null"/> when the code
    /// takes none. Never include PII such as passwords or full token values.
    /// </summary>
    IReadOnlyDictionary<string, object?>? Params { get; }
}
