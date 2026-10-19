namespace PointLurker.Core.Domain.Stores;

public sealed class NotificationItem
{
    private NotificationItem() { }

    public NotificationItem(
        Guid id,
        Guid notificationId,
        Guid storeItemId,
        string storeName,
        string itemName
    )
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            throw new ArgumentException("Store name cannot be empty.", nameof(storeName));
        }

        if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new ArgumentException("Item name cannot be empty.", nameof(itemName));
        }

        Id = id;
        NotificationId = notificationId;
        StoreItemId = storeItemId;
        StoreName = storeName;
        ItemName = itemName;
    }

    public Guid Id { get; private set; }
    public Guid NotificationId { get; private set; }
    public Guid StoreItemId { get; private set; }
    public string StoreName { get; private set; } = string.Empty;
    public string ItemName { get; private set; } = string.Empty;
}
