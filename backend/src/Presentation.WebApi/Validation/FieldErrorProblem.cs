using Microsoft.AspNetCore.Mvc;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Errors;

namespace PointLurker.Presentation.WebApi.Validation;

/// <summary>
/// Builds the <c>validation.failed</c> envelope every per-field failure travels in. Three producers
/// project into it — <see cref="ValidationFailureMapper"/> for request bodies, <c>SearchQuery</c> for
/// query strings, and <c>IdentityErrorMapper</c> for the framework Identity endpoints — so the entry
/// shape is settled in one place and the frontend resolves all three through one branch.
/// </summary>
internal static class FieldErrorProblem
{
    public static ProblemDetails Create(IReadOnlyList<FieldError> errors) =>
        ProblemDetailsFactory.Create(
            new DomainError(
                ErrorCodes.Validation.Failed,
                new Dictionary<string, object?> { ["errors"] = errors.Select(ToEntry).ToArray() }
            ),
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred."
        );

    private static Dictionary<string, object?> ToEntry(FieldError error) =>
        new()
        {
            ["field"] = error.Field,
            ["code"] = error.Code,
            ["params"] = ToEntryParams(error),
        };

    /// <summary>
    /// Every entry repeats its field inside <c>params</c>: that is where a catalog sentence reads it
    /// from, and guaranteeing it here is what stops each producer having to remember.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> ToEntryParams(FieldError error)
    {
        var entryParams = new Dictionary<string, object?> { ["field"] = error.Field };

        if (error.Params is null)
        {
            return entryParams;
        }

        foreach (var (key, value) in error.Params)
        {
            entryParams[key] = value;
        }

        return entryParams;
    }
}
