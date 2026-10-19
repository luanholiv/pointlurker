using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Presentation.WebApi.Errors;

/// <summary>
/// Catches unhandled exceptions and returns a ProblemDetails response.
/// <see cref="BadHttpRequestException"/> is how the framework reports that the request itself
/// could not be read (malformed body, oversized body); it carries the status the caller should
/// see, so that status is honoured and the fault is logged as a warning instead of polluting the
/// error stream. Everything else is a server fault: <c>500</c> with
/// <c>code = "system.internal_error"</c>. The exception message is never surfaced to the client —
/// only logged.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var (status, code, title) = Describe(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogUnhandledException(
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception
            );
        }
        else
        {
            logger.LogUnreadableRequest(
                httpContext.Request.Method,
                httpContext.Request.Path,
                status,
                exception
            );
        }

        var pd = ProblemDetailsFactory.Create(new DomainError(code), status, title);
        pd.Instance = httpContext.Request.Path;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        // Flatten Extensions (code, params) into the top-level JSON object alongside
        // standard ProblemDetails fields. WriteAsJsonAsync would override the ContentType
        // header and doesn't flatten extensions, so we serialise manually.
        var body = new Dictionary<string, object?>(pd.Extensions)
        {
            ["type"] = pd.Type,
            ["title"] = pd.Title,
            ["status"] = pd.Status,
            ["instance"] = pd.Instance,
        };

        await System.Text.Json.JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            body,
            cancellationToken: cancellationToken
        );
        return true;
    }

    /// <summary>
    /// Maps an exception to the response status, error code, and English developer-facing title.
    /// A <see cref="BadHttpRequestException"/> whose status is not 4xx does not describe a caller
    /// mistake, so it falls through to the server-fault branch.
    /// </summary>
    private static (int Status, string Code, string Title) Describe(Exception exception) =>
        exception switch
        {
            BadHttpRequestException bad
                when bad.StatusCode == StatusCodes.Status413PayloadTooLarge => (
                bad.StatusCode,
                ErrorCodes.Request.BodyTooLarge,
                "The request body is too large"
            ),
            BadHttpRequestException bad when IsClientError(bad.StatusCode) => (
                bad.StatusCode,
                ErrorCodes.Request.MalformedBody,
                "The request could not be read"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.System.InternalError,
                "An unexpected error occurred"
            ),
        };

    private static bool IsClientError(int status) => status is >= 400 and < 500;
}
