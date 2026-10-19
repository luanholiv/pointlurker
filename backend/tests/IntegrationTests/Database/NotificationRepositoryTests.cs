using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Database;

[Collection(ContainersCollection.Name)]
public sealed class NotificationRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private NotificationRepository _sut = null!;
    private IUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        _db = new AppDbContext(options);
        _sut = new NotificationRepository(_db);
        _unitOfWork = new UnitOfWork(_db);
    }

    private async Task AddAndCommitAsync(Notification notification)
    {
        _sut.Add(notification);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ListByUserAsync_returns_empty_when_no_notifications()
    {
        var result = await _sut.ListByUserAsync(Guid.NewGuid());
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_persists_notification_with_items()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId);
        notification.AddItem(
            new NotificationItem(
                Guid.NewGuid(),
                notification.Id,
                Guid.NewGuid(),
                "xQc",
                "VIP Badge"
            )
        );

        await AddAndCommitAsync(notification);

        var saved = await _sut.ListByUserAsync(userId);
        saved.Should().ContainSingle(n => n.Id == notification.Id);
        saved.Single().Items.Should().ContainSingle(i => i.ItemName == "VIP Badge");
    }

    [Fact]
    public async Task ListByUserAsync_returns_only_notifications_for_given_user()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        await AddAndCommitAsync(CreateNotification(userId1));
        await AddAndCommitAsync(CreateNotification(userId2));

        var result = await _sut.ListByUserAsync(userId1);

        result.Should().ContainSingle();
        result.Single().UserId.Should().Be(userId1);
    }

    [Fact]
    public async Task ListByUserAsync_includes_items_for_each_notification()
    {
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId);
        var itemId = Guid.NewGuid();
        notification.AddItem(
            new NotificationItem(itemId, notification.Id, Guid.NewGuid(), "Store", "Item")
        );

        await AddAndCommitAsync(notification);

        var loaded = await _sut.ListByUserAsync(userId);
        loaded.Single().Items.Should().ContainSingle(i => i.Id == itemId);
    }

    private static Notification CreateNotification(Guid userId) =>
        new(Guid.NewGuid(), userId, NotificationChannel.Email);
}
