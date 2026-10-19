using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

[ExcludeFromCodeCoverage]
internal static partial class SyncStoresLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "No user found. Skipping sync cycle."
    )]
    public static partial void LogNoUserFound(this ILogger logger);

    // EventId 1002 retired (LogNoStreamElementsToken) — numbering intentionally left with a gap.

    // EventId 1003 retired (unused LogDecryptTokenFailed) — numbering intentionally left with a gap.

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "No active stores to sync."
    )]
    public static partial void LogNoActiveStores(this ILogger logger);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Error,
        Message = "Failed to sync store {StoreId} ({DisplayName})."
    )]
    public static partial void LogSyncStoreFailed(
        this ILogger logger,
        Guid storeId,
        string displayName,
        Exception ex
    );

    // EventId 1006 retired (unused LogRedeemItemFailed) — numbering intentionally left with a gap.

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Error,
        Message = "Failed to send notification email."
    )]
    public static partial void LogSendNotificationFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Debug,
        Message = "Auto-redeem attempted for wishlist entry {WishlistEntryId} (item {StoreItemId})."
    )]
    public static partial void LogAutoRedeemAttempted(
        this ILogger logger,
        Guid wishlistEntryId,
        Guid storeItemId
    );

    [LoggerMessage(
        EventId = 1009,
        Level = LogLevel.Debug,
        Message = "Auto-redeem skipped for wishlist entry {WishlistEntryId}: cooldown not elapsed."
    )]
    public static partial void LogAutoRedeemSkippedCooldown(
        this ILogger logger,
        Guid wishlistEntryId
    );

    [LoggerMessage(
        EventId = 1010,
        Level = LogLevel.Debug,
        Message = "Auto-redeem skipped for wishlist entry {WishlistEntryId}: redemption already inflight."
    )]
    public static partial void LogAutoRedeemSkippedInflight(
        this ILogger logger,
        Guid wishlistEntryId
    );

    [LoggerMessage(
        EventId = 1011,
        Level = LogLevel.Information,
        Message = "Auto-redeem created Pending redemption {RedemptionId} for wishlist entry {WishlistEntryId} (item {StoreItemId})."
    )]
    public static partial void LogAutoRedeemCreatedPending(
        this ILogger logger,
        Guid redemptionId,
        Guid wishlistEntryId,
        Guid storeItemId
    );

    [LoggerMessage(
        EventId = 1012,
        Level = LogLevel.Information,
        Message = "Wishlist entry {WishlistEntryId} blocked: missing input coverage for {MissingLabels}."
    )]
    public static partial void LogWishlistEntryBlockedOnInput(
        this ILogger logger,
        Guid wishlistEntryId,
        string missingLabels
    );

    [LoggerMessage(
        EventId = 1013,
        Level = LogLevel.Information,
        Message = "Wishlist entry {WishlistEntryId} input coverage restored."
    )]
    public static partial void LogWishlistEntryInputCoverageRestored(
        this ILogger logger,
        Guid wishlistEntryId
    );

    [LoggerMessage(
        EventId = 1014,
        Level = LogLevel.Warning,
        Message = "Auto-redeem skipped for wishlist entry {WishlistEntryId}: input resolution or decryption failed."
    )]
    public static partial void LogAutoRedeemSkippedInputFailure(
        this ILogger logger,
        Guid wishlistEntryId
    );

    [LoggerMessage(
        EventId = 1015,
        Level = LogLevel.Warning,
        Message = "Auto-redeem skipped for {Provider}: no account name recorded for its credential; cannot verify point balance this cycle."
    )]
    public static partial void LogAutoRedeemNoAccountName(
        this ILogger logger,
        StoreProvider provider
    );

    [LoggerMessage(
        EventId = 1016,
        Level = LogLevel.Warning,
        Message = "Auto-redeem skipped for {Provider}: failed to decrypt its credential; cannot verify point balance this cycle."
    )]
    public static partial void LogAutoRedeemCredentialDecryptFailed(
        this ILogger logger,
        StoreProvider provider,
        Exception ex
    );

    [LoggerMessage(
        EventId = 1017,
        Level = LogLevel.Warning,
        Message = "Auto-redeem skipped channel {ChannelId}: failed to fetch point balance this cycle."
    )]
    public static partial void LogAutoRedeemBalanceFetchFailed(
        this ILogger logger,
        string channelId,
        Exception ex
    );

    [LoggerMessage(
        EventId = 1018,
        Level = LogLevel.Debug,
        Message = "Auto-redeem skipped for wishlist entry {WishlistEntryId}: insufficient points (cost {Cost}, available {Available})."
    )]
    public static partial void LogAutoRedeemSkippedInsufficientPoints(
        this ILogger logger,
        Guid wishlistEntryId,
        long cost,
        long available
    );

    [LoggerMessage(
        EventId = 1019,
        Level = LogLevel.Information,
        Message = "Pruned {DeletedRows} superseded resolution log rows older than {Cutoff}."
    )]
    public static partial void LogResolutionLogPruned(
        this ILogger logger,
        int deletedRows,
        DateTimeOffset cutoff
    );

    [LoggerMessage(
        EventId = 1020,
        Level = LogLevel.Error,
        Message = "Failed to prune the resolution log."
    )]
    public static partial void LogResolutionLogPruneFailed(this ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 1021,
        Level = LogLevel.Error,
        Message = "Cannot send the availability notification: the account has no email address."
    )]
    public static partial void LogNotificationRecipientMissing(this ILogger logger);

    [LoggerMessage(
        EventId = 1022,
        Level = LogLevel.Debug,
        Message = "Auto-redeem skipped for wishlist entry {WishlistEntryId}: {QuotaUsed} of {DesiredRedemptions} desired redemptions already held."
    )]
    public static partial void LogAutoRedeemSkippedQuotaReached(
        this ILogger logger,
        Guid wishlistEntryId,
        int quotaUsed,
        int desiredRedemptions
    );

    [LoggerMessage(
        EventId = 1023,
        Level = LogLevel.Warning,
        Message = "Auto-redeem skipped for {Provider}: no credential configured; cannot verify point balance this cycle."
    )]
    public static partial void LogAutoRedeemNoCredential(
        this ILogger logger,
        StoreProvider provider
    );

    [LoggerMessage(
        EventId = 1024,
        Level = LogLevel.Warning,
        Message = "Auto-redeem skipped for {Provider}: its credential has expired; paste a new one to resume."
    )]
    public static partial void LogAutoRedeemCredentialExpired(
        this ILogger logger,
        StoreProvider provider
    );
}
