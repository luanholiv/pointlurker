using PointLurker.Core.Application.Errors;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Presentation.WebApi.Errors;

/// <summary>
/// Projects an <see cref="IApplicationError"/> into an RFC 7807 response. The error already
/// carries its canonical code and params, so this is a straight projection — no second code
/// taxonomy, and nothing is recovered by parsing the English title.
/// </summary>
public static class ErrorMapping
{
    private static readonly IReadOnlyDictionary<string, object?> NoParams =
        new Dictionary<string, object?>();

    public static IResult ToHttpResult<TError>(this TError error)
        where TError : IApplicationError =>
        Results.Problem(
            ProblemDetailsFactory.Create(
                new DomainError(error.Code, error.Params ?? NoParams),
                ToStatus(error.Kind),
                error.Title
            )
        );

    private static int ToStatus(ErrorKind kind) =>
        kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Unprocessable => StatusCodes.Status422UnprocessableEntity,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
}
