using Microsoft.Extensions.Logging;

namespace PointLurker.Core.Application.Stores;

public static partial class RefreshStoreChannelInfoLogs
{
    [LoggerMessage(
        EventId = 1021,
        Level = LogLevel.Warning,
        Message = "Failed to refresh channel info for store {StoreId} (channel {ChannelId}); keeping previous avatar and display name."
    )]
    public static partial void LogChannelRefreshFailed(
        this ILogger logger,
        Guid storeId,
        string channelId,
        Exception ex
    );

    [LoggerMessage(
        EventId = 1022,
        Level = LogLevel.Information,
        Message = "Refreshed channel info for {StoreCount} store(s)."
    )]
    public static partial void LogChannelInfoRefreshed(this ILogger logger, int storeCount);
}
