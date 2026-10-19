namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The query string of <c>GET /api/stores</c>. Raw strings for the same reason
/// <see cref="StoreItemsQuery"/> gives.
/// </summary>
internal sealed record StoreListQuery(string? Q, string? Active, string? Offset, string? Limit);
