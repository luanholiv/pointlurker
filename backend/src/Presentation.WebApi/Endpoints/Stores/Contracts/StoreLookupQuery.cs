namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The query string of <c>GET /api/stores/lookup</c>. One of the few query contracts with rules of
/// its own, in <c>StoreLookupQueryValidator</c>: the provider must be one a store can be added for,
/// and a provider with many stores needs a query to pick one.
/// </summary>
/// <param name="Provider">The provider slug, as in the credential routes.</param>
/// <param name="Query">What the owner typed — for StreamElements, the channel username.</param>
internal sealed record StoreLookupQuery(string? Provider, string? Query);
