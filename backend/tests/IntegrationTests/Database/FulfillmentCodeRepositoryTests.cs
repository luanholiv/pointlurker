using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Database;

[Collection(ContainersCollection.Name)]
public sealed class FulfillmentCodeRepositoryTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private AppDbContext _db = null!;
    private FulfillmentCodeRepository _sut = null!;
    private IUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _db = NewContext();
        _sut = new FulfillmentCodeRepository(_db);
        _unitOfWork = new UnitOfWork(_db);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task A_due_code_comes_back_with_its_redemption_snapshot()
    {
        var redemption = await AddRedemptionAsync("Steam R$ 50", "GauPoints");
        await AddCodeAsync(redemption.Id);

        var due = await _sut.GetDueForUpdateAsync(Now);

        var delivery = due.Should().ContainSingle().Subject;
        delivery.Code.RedemptionId.Should().Be(redemption.Id);
        delivery.StoreItemId.Should().Be(redemption.StoreItemId);
        delivery.StoreItemName.Should().Be("Steam R$ 50");
        delivery.StoreName.Should().Be("GauPoints");
    }

    [Fact]
    public async Task Delivered_codes_and_codes_not_yet_due_are_left_out()
    {
        var sent = await AddCodeAsync((await AddRedemptionAsync()).Id);
        var later = await AddCodeAsync((await AddRedemptionAsync()).Id);
        var due = await AddCodeAsync((await AddRedemptionAsync()).Id);
        await using (var write = NewContext())
        {
            var repo = new FulfillmentCodeRepository(write);
            var rows = await repo.GetDueForUpdateAsync(Now);
            rows.Single(r => r.Code.Id == sent.Id).Code.MarkSent(Now);
            rows.Single(r => r.Code.Id == later.Id)
                .Code.RecordFailedAttempt("SMTP 451", Now.AddMinutes(5));
            rows.Single(r => r.Code.Id == due.Id)
                .Code.RecordFailedAttempt("SMTP 451", Now.AddMinutes(-1));
            await new UnitOfWork(write).SaveChangesAsync();
        }

        var result = await _sut.GetDueForUpdateAsync(Now);

        result.Select(r => r.Code.Id).Should().Equal(due.Id);
    }

    /// <summary>The delivery writes through the rows it read, so the read has to track them.</summary>
    [Fact]
    public async Task An_outcome_recorded_on_a_due_code_is_persisted()
    {
        await AddCodeAsync((await AddRedemptionAsync()).Id);
        var delivery = (await _sut.GetDueForUpdateAsync(Now)).Single();

        delivery.Code.MarkSent(Now);
        _sut.Update(delivery.Code);
        await _unitOfWork.SaveChangesAsync();

        await using var read = NewContext();
        (await read.FulfillmentCodes.SingleAsync()).SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task A_second_code_for_one_redemption_is_refused()
    {
        var redemption = await AddRedemptionAsync();
        await AddCodeAsync(redemption.Id);

        var act = () => AddCodeAsync(redemption.Id);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task A_code_for_no_redemption_is_refused()
    {
        var act = () => AddCodeAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Updating_an_untracked_code_throws()
    {
        var code = new FulfillmentCode(Guid.NewGuid(), Guid.NewGuid(), "ct", Now);

        var act = () => _sut.Update(code);

        act.Should().Throw<InvalidOperationException>();
    }

    private AppDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.ConnectionString).Options
        );

    /// <summary>Written through a context of its own, so a refused insert leaves the shared one clean.</summary>
    private async Task<FulfillmentCode> AddCodeAsync(Guid redemptionId)
    {
        var code = new FulfillmentCode(Guid.NewGuid(), redemptionId, "ct:gift", Now.AddMinutes(-2));
        await using var write = NewContext();
        new FulfillmentCodeRepository(write).Add(code);
        await new UnitOfWork(write).SaveChangesAsync();
        return code;
    }

    private async Task<Redemption> AddRedemptionAsync(
        string itemName = "Item",
        string storeName = "Store"
    )
    {
        var store = new WatchedStore(
            Guid.NewGuid(),
            StoreProvider.GauPoints,
            new StoreExternalId("gaupoints"),
            storeName,
            Guid.NewGuid(),
            Now
        );
        var item = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            new StoreItemApiData(
                StoreProvider.GauPoints,
                new ProviderItemId("gp-" + Guid.NewGuid().ToString("N")),
                new StoreExternalId("gaupoints"),
                itemName,
                null,
                "perk",
                100,
                "default",
                true,
                false,
                true,
                false,
                false,
                [],
                [],
                new ItemQuantity(5, 5),
                new ItemCooldown(0, 0, 0),
                Now,
                null,
                Now
            )
        );
        item.ClearDomainEvents();
        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);
        var redemption = entry.CreateRedemption(item, null, itemName, storeName, Now);

        await using var write = NewContext();
        write.WatchedStores.Add(store);
        write.StoreItems.Add(item);
        write.WishlistEntries.Add(entry);
        write.Redemptions.Add(redemption);
        await write.SaveChangesAsync();
        return redemption;
    }
}
