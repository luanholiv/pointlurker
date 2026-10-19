using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Presentation.WebApi.Errors;

/// <summary>
/// Creates RFC 7807 <see cref="ProblemDetails"/> instances from <see cref="DomainError"/> values.
/// The <c>code</c> and <c>params</c> extensions are always included so the frontend
/// can resolve the localized message from its catalog.
/// </summary>
public static class ProblemDetailsFactory
{
    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from a <see cref="DomainError"/>.
    /// </summary>
    /// <param name="error">The domain error carrying the stable code and interpolation params.</param>
    /// <param name="status">HTTP status code (e.g., 400, 404, 422).</param>
    /// <param name="title">English developer-facing fallback. Never a user-facing localized string.</param>
    public static ProblemDetails Create(DomainError error, int status, string title)
    {
        var pd = new ProblemDetails
        {
            Type = BuildTypeUri(error.Code),
            Title = title,
            Status = status,
        };

        pd.Extensions["code"] = error.Code;
        pd.Extensions["params"] = error.Params;

        return pd;
    }

    private static string BuildTypeUri(string code) =>
        $"https://errors.app/{code.Replace('.', '/')}";
}
