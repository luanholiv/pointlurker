using Core.Domain.UnitTests.Generators;
using CsCheck;
using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class WishlistEntryRedemptionPropertyTests
{
    /// <summary>
    /// Nothing held and no rate floor, so each property below is about the gate it names.
    /// </summary>
    private static readonly RedemptionPolicy Unrestricted = new(
        QuotaUsed: 0,
        MinimumInterval: TimeSpan.Zero
    );

    [Fact]
    public void CanCreateRedemption_equals_the_conjunction_of_its_gates()
    {
        var cooldownPair = Gen.Select(
            Gen.Long[0, 2_000_000],
            Gen.Long[0, 2_000_000],
            (user, global) => (User: user, Global: global)
        );

        Gen.Select(
                DomainGen.Snapshots, // availability inputs
                Gen.Long[0, 1000], // cost
                Gen.Long[0, 1000], // available balance
                cooldownPair, // user/global cooldown ms
                Gen.Bool, // auto-redeem
                Gen.Long[-1, 2_000_000], // last-redemption ms (-1 == no prior redemption)
                Gen.Long[0, 2_000_000] // now ms
            )
            .Sample(
                (snapshot, cost, balance, cooldown, autoRedeem, lastMs, nowMs) =>
                {
                    var item = BuildItem(snapshot, cost, cooldown.User, cooldown.Global);
                    var entry = NewEntry();
                    if (autoRedeem)
                    {
                        entry.EnableAutoRedeem(DateTimeOffset.UnixEpoch);
                    }

                    var now = DateTimeOffset.UnixEpoch.AddMilliseconds(nowMs);
                    DateTimeOffset? last = null;
                    if (lastMs >= 0)
                    {
                        last = DateTimeOffset.UnixEpoch.AddMilliseconds(lastMs);
                        entry.CreateRedemption(item, null, "item", "store", last.Value);
                    }

                    var cooldownMs = Math.Max(cooldown.User, cooldown.Global);
                    var available = ItemAvailability
                        .From(snapshot.Enabled, snapshot.Quantity)
                        .IsAvailable;
                    var expected =
                        autoRedeem
                        && available
                        && cost <= balance
                        && (last is null || last.Value.AddMilliseconds(cooldownMs) <= now);

                    entry
                        .CanCreateRedemption(item, now, balance, Unrestricted)
                        .Should()
                        .Be(expected);
                }
            );
    }

    // The cooldown comparison is `unblocksAt <= now`, so an entry whose unblock instant lands
    // exactly on `now` must be eligible.
    [Fact]
    public void Cooldown_boundary_is_inclusive()
    {
        Gen.Select(Gen.Long[0, 2_000_000], Gen.Long[0, 2_000_000], Gen.Long[0, 2_000_000])
            .Sample(
                (cdUser, cdGlobal, lastMs) =>
                {
                    var item = BuildItem(
                        (Enabled: true, Quantity: new ItemQuantity(-1, -1)),
                        cost: 0,
                        cdUser,
                        cdGlobal
                    );
                    var entry = NewEntry();
                    entry.EnableAutoRedeem(DateTimeOffset.UnixEpoch);

                    var last = DateTimeOffset.UnixEpoch.AddMilliseconds(lastMs);
                    entry.CreateRedemption(item, null, "item", "store", last);

                    var cooldownMs = Math.Max(cdUser, cdGlobal);
                    var now = last.AddMilliseconds(cooldownMs); // unblocksAt == now exactly

                    entry
                        .CanCreateRedemption(item, now, availableBalance: 0, Unrestricted)
                        .Should()
                        .BeTrue();
                }
            );
    }

    private static WishlistEntry NewEntry() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UnixEpoch);

    private static StoreItem BuildItem(
        (bool Enabled, ItemQuantity Quantity) snapshot,
        long cost,
        long cooldownUserMs,
        long cooldownGlobalMs
    ) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-1"),
                new StoreExternalId("ch-1"),
                "Test Item",
                null,
                "perk",
                cost,
                "default",
                snapshot.Enabled,
                false,
                true,
                false,
                false,
                [],
                [],
                snapshot.Quantity,
                new ItemCooldown(cooldownUserMs, cooldownGlobalMs, 0),
                DateTimeOffset.UnixEpoch,
                null,
                DateTimeOffset.UnixEpoch
            )
        );
}
