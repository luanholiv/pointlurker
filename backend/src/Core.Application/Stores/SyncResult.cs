namespace PointLurker.Core.Application.Stores;

/// <param name="StoresSkipped">
/// Active stores left alone because their provider's minimum sync interval had not passed. Not a
/// failure, so kept apart from <paramref name="StoresSynced"/>.
/// </param>
/// <param name="ItemsProcessed">Item rows the cycle looked at, across every store.</param>
/// <param name="ItemsChanged">How many of those the cycle actually altered.</param>
public sealed record SyncResult(
    int StoresSynced,
    int StoresSkipped,
    int ItemsProcessed,
    int ItemsChanged,
    int RedemptionsAttempted,
    int RedemptionsPending,
    int NotificationsSent
);
