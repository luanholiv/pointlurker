namespace PointLurker.Core.Application.Stores;

/// <summary>
/// A user's wishlist as pages of store groups. One row <b>is</b> a store group, so the store's
/// fields are carried once alongside its matching entries rather than repeated on each of them —
/// the envelope constrains the envelope, not what a row is.
/// </summary>
/// <remarks>
/// This list is NOT paged: it is always served whole, so <see cref="Offset" /> is always 0 and
/// <see cref="Limit" /> always equals <see cref="TotalCount" />. The bounds are structural — every
/// list answers with the same envelope, and that shape is arch-test enforced — not a description of
/// a slice that was taken. Bounds sent by a caller therefore change nothing, which
/// <c>WishlistEndpointTests.Slice_bounds_are_structural_and_the_list_is_served_whole</c> pins.
/// </remarks>
/// <param name="TotalCount">
/// Stores retaining at least one entry after filtering. Filters still evaluate against entries; a
/// store whose entries are all excluded contributes no row and is not counted.
/// </param>
public sealed record WishlistGroupedResponse(
    IReadOnlyList<WishlistStoreGroup> Items,
    int TotalCount,
    int Offset,
    int Limit
);
