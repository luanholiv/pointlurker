using FluentValidation;

namespace PointLurker.Presentation.WebApi.Validation;

/// <summary>
/// Runs the contract's validator before the handler and answers a failure with the
/// <c>validation.failed</c> body. This is the reachable equivalent of MVC's
/// <c>InvalidModelStateResponseFactory</c>, which cannot fire in a minimal-API application.
/// </summary>
/// <remarks>
/// A body that could not be bound at all never reaches here: minimal APIs throw
/// <see cref="BadHttpRequestException"/> while binding, and the global handler answers that with
/// <c>request.malformed_body</c>. So this filter only ever sees a request that parsed, which is
/// exactly the split <c>docs/ERROR-CODES.md</c> describes.
/// </remarks>
internal sealed class ValidationEndpointFilter<TRequest>(IValidator<TRequest> validator)
    : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        // A handler may take the contract alongside route values and services. If this invocation
        // carries no argument of the validated type there is nothing to check, and rejecting the
        // call would be wrong.
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : Results.Problem(ValidationFailureMapper.ToProblemDetails(result));
    }
}
