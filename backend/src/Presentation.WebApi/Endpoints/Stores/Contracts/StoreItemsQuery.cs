namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The query string of <c>GET /api/stores/{id}/items</c>.
/// </summary>
/// <remarks>
/// Every filter is a raw string rather than a typed property. That is deliberate: it puts the
/// framework's model binder out of the decision, so <c>min=abc</c> answers with this app's
/// <c>{code, params}</c> contract instead of an empty framework 400 the frontend catalog cannot
/// render — a typed property would be rejected during binding, before any filter or handler runs.
/// The property names are the ones the browser URL already carries, so a bookmarked filtered view
/// keeps resolving; they are declared once in <c>SearchParameters</c>.
/// </remarks>
internal sealed record StoreItemsQuery(
    string? Q,
    string? Avail,
    string? Sub,
    string? Feat,
    string? Cat,
    string? Min,
    string? Max,
    string? Sort,
    string? Dir,
    string? Offset,
    string? Limit
);
