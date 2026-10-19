namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// The orderings applied to the entries inside one wishlist store row. The rows themselves are
/// always ordered by store display name, which is not a caller's choice.
/// </summary>
public enum WishlistEntrySortField
{
    ItemName,
    ItemCost,

    /// <summary>When the entry was added to the wishlist.</summary>
    CreatedAt,
}
