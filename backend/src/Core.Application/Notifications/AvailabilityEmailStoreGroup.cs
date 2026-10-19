namespace PointLurker.Core.Application.Notifications;

/// <summary>
/// One store's newly available items. <paramref name="WatchedStoreId"/> is carried so the email
/// can link an item back to the store page it lives on.
/// </summary>
public sealed record AvailabilityEmailStoreGroup(
    string StoreName,
    Guid WatchedStoreId,
    IReadOnlyList<AvailabilityEmailItem> Items
);
