using CsCheck;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Generators;

/// <summary>
/// Reusable CsCheck generators for the <c>Core.Domain</c> property tests.
/// Ranges intentionally span the <c>-1</c> unlimited sentinel and the zero boundary
/// so generated samples exercise the availability edges, not just the happy path.
/// </summary>
public static class DomainGen
{
    private const int MaxQuantity = 1000;
    private const long MaxCooldownMs = 86_400_000; // 24h in ms — generous upper bound

    /// <summary>Valid <see cref="ItemQuantity"/>: both fields in <c>[-1, MaxQuantity]</c>.</summary>
    public static readonly Gen<ItemQuantity> Quantities = Gen.Select(
        Gen.Int[-1, MaxQuantity],
        Gen.Int[-1, MaxQuantity],
        (total, current) => new ItemQuantity(total, current)
    );

    /// <summary>Valid <see cref="ItemCooldown"/>: all three fields in <c>[0, MaxCooldownMs]</c>.</summary>
    public static readonly Gen<ItemCooldown> Cooldowns = Gen.Select(
        Gen.Long[0, MaxCooldownMs],
        Gen.Long[0, MaxCooldownMs],
        Gen.Long[0, MaxCooldownMs],
        (user, global, category) => new ItemCooldown(user, global, category)
    );

    /// <summary>An availability-relevant snapshot: the only two inputs that drive availability.</summary>
    public static readonly Gen<(bool Enabled, ItemQuantity Quantity)> Snapshots = Gen.Select(
        Gen.Bool,
        Quantities,
        (enabled, quantity) => (Enabled: enabled, Quantity: quantity)
    );

    /// <summary>A bounded sequence of snapshots to fold through <see cref="StoreItem.UpdateFromApi"/>.</summary>
    public static readonly Gen<List<(bool Enabled, ItemQuantity Quantity)>> Sequences =
        Snapshots.List[0, 16];

    /// <summary>A valid <see cref="StoreItemApiData"/> whose availability is driven by the snapshot.</summary>
    public static readonly Gen<StoreItemApiData> ApiData = Snapshots.Select(ToApiData);

    /// <summary>Builds a valid <see cref="StoreItemApiData"/> carrying the snapshot's enabled flag and quantity.</summary>
    public static StoreItemApiData ToApiData((bool Enabled, ItemQuantity Quantity) snapshot) =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId("se-1"),
            new StoreExternalId("ch-1"),
            "Test Item",
            null,
            "perk",
            100,
            "default",
            snapshot.Enabled,
            false,
            true,
            false,
            false,
            [],
            [],
            snapshot.Quantity,
            new ItemCooldown(0, 0, 0),
            DateTimeOffset.UnixEpoch,
            null,
            DateTimeOffset.UnixEpoch
        );
}
