namespace PointLurker.Core.Domain.Stores;

public sealed class Notification
{
    private readonly List<NotificationItem> _items = [];

    private Notification() { }

    public Notification(Guid id, Guid userId, NotificationChannel channel)
    {
        Id = id;
        UserId = userId;
        Channel = channel;
        Status = NotificationStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string? FailureReason { get; private set; }

    public IReadOnlyCollection<NotificationItem> Items => _items;

    public void AddItem(NotificationItem item) => _items.Add(item);

    public void MarkSent(DateTimeOffset sentAt)
    {
        SentAt = sentAt;
        Status = NotificationStatus.Sent;
        FailureReason = null;
    }

    public void MarkFailed(string failureReason)
    {
        Status = NotificationStatus.Failed;
        FailureReason = failureReason;
    }
}
