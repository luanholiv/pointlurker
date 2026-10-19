namespace PointLurker.Core.Application.Errors;

/// <summary>
/// Transport-free classification of an application failure. The presentation layer maps each
/// kind to an HTTP status code so the application layer stays free of HTTP concerns.
/// </summary>
public enum ErrorKind
{
    /// <summary>The addressed resource does not exist.</summary>
    NotFound,

    /// <summary>The request conflicts with the current state of the resource.</summary>
    Conflict,

    /// <summary>The request is well-formed but violates a business rule.</summary>
    Unprocessable,
}
