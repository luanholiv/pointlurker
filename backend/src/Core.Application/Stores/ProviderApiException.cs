namespace PointLurker.Core.Application.Stores;

/// <summary>A provider call that failed: an error response, or no response at all.</summary>
public sealed class ProviderApiException(
    int statusCode,
    string? reasonPhrase,
    string message,
    string? content = null,
    Exception? innerException = null
) : Exception(message, innerException)
{
    /// <summary>
    /// <see cref="StatusCode"/> for a request that never reached the provider, so there is no
    /// response to report. Distinct from every real HTTP status, which starts at 100.
    /// </summary>
    public const int NoResponse = 0;

    public int StatusCode { get; } = statusCode;
    public string? ReasonPhrase { get; } = reasonPhrase;

    /// <summary>Raw response body from the provider, when available. Diagnostic only — never user-facing.</summary>
    public string? Content { get; } = content;

    /// <summary>True when the request failed in transport and the provider never answered.</summary>
    public bool IsUnreachable => StatusCode == NoResponse;
}
