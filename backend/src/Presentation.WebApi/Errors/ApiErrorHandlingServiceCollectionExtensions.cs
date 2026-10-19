namespace PointLurker.Presentation.WebApi.Errors;

public static class ApiErrorHandlingServiceCollectionExtensions
{
    /// <summary>
    /// Wires the RFC 7807 error surface: unhandled exceptions become a
    /// <c>system.internal_error</c> problem response, and a request that could not be read becomes
    /// <c>request.malformed_body</c>.
    /// </summary>
    /// <remarks>
    /// Per-field validation is not wired here. It belongs to the endpoints that bind a body, via
    /// <c>ValidatesBody&lt;TRequest&gt;()</c> and <c>AddRequestValidation()</c> — an
    /// <c>IEndpointFilter</c> rather than MVC's <c>InvalidModelStateResponseFactory</c>, which
    /// fires only for controller actions and so could never run in this application.
    /// </remarks>
    public static IServiceCollection AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}
