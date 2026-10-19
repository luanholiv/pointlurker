namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

/// <summary>
/// The query string of <c>GET /api/wishlist</c>. Raw strings so the endpoint, not the model binder,
/// decides what an uninterpretable filter answers with. Unpaged — the grouped result is the whole
/// list — so there is no <c>offset</c> or <c>limit</c> here.
/// </summary>
internal sealed record WishlistListQuery(
    string? Q,
    string? Avail,
    string? Auto,
    string? Notify,
    string? Blocked,
    string? Store,
    string? Sort,
    string? Dir
);
