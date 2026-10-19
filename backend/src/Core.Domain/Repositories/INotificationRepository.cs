using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

public interface INotificationRepository
{
    /// <summary>Stages the notification and its items for insertion.</summary>
    void Add(Notification notification);

    Task<IReadOnlyList<Notification>> ListByUserAsync(Guid userId, CancellationToken ct = default);
}
