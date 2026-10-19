namespace PointLurker.Core.Domain.Stores.Enums;

/// <summary>
/// The upstream service a store, its items and its redemptions belong to. Persisted as
/// <see langword="int"/> in <c>watched_stores</c>, <c>store_items</c> and <c>redemptions</c>.
/// <para>
/// <b>APPEND-ONLY:</b> New values get the next unused int.
/// NEVER reorder, renumber, or remove existing values —
/// stored rows carry the integer, and redemption rows are kept after their store is removed.
/// A unit test (<c>StoreProviderTests.StoreProvider_int_to_name_mapping_is_unchanged</c>)
/// snapshots the int→name map and fails the build on any violation.
/// </para>
/// </summary>
public enum StoreProvider
{
    StreamElements = 0,
    GauPoints = 1,
}
