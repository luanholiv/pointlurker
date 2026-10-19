namespace PointLurker.Presentation.WebApi.Validation;

internal static class ValidationRouteHandlerBuilderExtensions
{
    /// <summary>
    /// Validates the request body against the contract's registered validator before the handler
    /// runs. Declared per route rather than per group: only some endpoints bind a body, and a group
    /// -wide filter would run for the ones that do not.
    /// </summary>
    public static RouteHandlerBuilder ValidatesBody<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : class => builder.AddEndpointFilter<ValidationEndpointFilter<TRequest>>();

    /// <summary>
    /// Validates the <c>[AsParameters]</c> query contract against its registered validator before
    /// the handler runs. The same filter serves both surfaces because it selects its argument by
    /// type, not by position or source — a query record is found in the invocation exactly as a body
    /// is. Named apart from <see cref="ValidatesBody{TRequest}"/> so a route says which surface it
    /// is guarding.
    /// </summary>
    public static RouteHandlerBuilder ValidatesQuery<TQuery>(this RouteHandlerBuilder builder)
        where TQuery : class => builder.AddEndpointFilter<ValidationEndpointFilter<TQuery>>();
}
