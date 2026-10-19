namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// Narrowing and ordering for the wishlist. Filters evaluate against entries; a store keeps its
/// row only while at least one of its entries survives them. Every nullable member is a filter to
/// skip when null.
/// </summary>
/// <param name="SearchTerm">
/// Free text matched against the wishlisted item's name. Not a substring test: the term is
/// tokenized, so its words match in any order and independently of accents, and a single
/// misspelled word still finds its item. The description is not matched — the wishlist does not
/// render it, so a row found by it would carry no visible reason for being there.
/// </param>
/// <param name="AvailableOnly">Restrict to entries whose item is available when <c>true</c>.</param>
/// <param name="AutoRedeem">Restrict by the auto-redeem flag.</param>
/// <param name="NotifyOnAvailable">Restrict by the notify-on-available flag.</param>
/// <param name="BlockedOnInput">Restrict by the persisted blocked-on-input flag, never a per-read recompute.</param>
/// <param name="StoreId">Restrict to one store's entries.</param>
public sealed record WishlistEntryFilter(
    string? SearchTerm,
    bool? AvailableOnly,
    bool? AutoRedeem,
    bool? NotifyOnAvailable,
    bool? BlockedOnInput,
    Guid? StoreId,
    WishlistEntrySortField SortField,
    SortDirection SortDirection
);
