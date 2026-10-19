namespace PointLurker.Core.Application.Stores;

/// <summary>
/// What a provider can and cannot do, stated as data so callers branch on a capability rather than
/// on which provider they hold.
/// </summary>
/// <param name="SupportsMessages">A redemption can carry a free-text message to the streamer.</param>
/// <param name="SupportsMultipleStores">More than one store can be watched; otherwise the provider is one fixed store.</param>
/// <param name="SupportsRedemptionPolling">A submitted redemption's outcome can be read back later.</param>
/// <param name="ReturnsFulfillmentCode">A successful redemption can answer with a code the owner needs.</param>
/// <param name="SupportsUnlimitedStock">Stock can be unlimited (<c>-1</c>) rather than always counted.</param>
/// <param name="ReportsTotalStock">Stock comes with a total as well as what is left.</param>
/// <param name="InputProfile">Where an item's required inputs come from.</param>
/// <param name="ProvidesChannelMetadata">
/// A store's display name and avatar can be looked up again; otherwise they are fixed constants
/// and the daily refresh leaves the store alone.
/// </param>
/// <param name="MinimumSyncInterval">
/// The least time between two fetches of one store. A sync tick skips a store it looked at more
/// recently than this, so one cron tuned for a large provider does not hammer a small one.
/// <see cref="TimeSpan.Zero"/> syncs on every tick.
/// </param>
public sealed record StoreProviderCapabilities(
    bool SupportsMessages,
    bool SupportsMultipleStores,
    bool SupportsRedemptionPolling,
    bool ReturnsFulfillmentCode,
    bool SupportsUnlimitedStock,
    bool ReportsTotalStock,
    InputProfileKind InputProfile,
    bool ProvidesChannelMetadata,
    TimeSpan MinimumSyncInterval
);
