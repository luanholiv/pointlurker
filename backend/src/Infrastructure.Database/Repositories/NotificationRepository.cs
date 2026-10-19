using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    public void Add(Notification notification) => dbContext.Notifications.Add(notification);

    public async Task<IReadOnlyList<Notification>> ListByUserAsync(
        Guid userId,
        CancellationToken ct = default
    )
    {
        return await dbContext
            .Notifications.Include(notification => notification.Items)
            .Where(notification => notification.UserId == userId)
            .ToListAsync(ct);
    }
}
