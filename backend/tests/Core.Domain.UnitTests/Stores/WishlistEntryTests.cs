using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class WishlistEntryTests
{
    /// <summary>
    /// Fixed instant: the entity reads no clock, so every timestamp in these tests is one the test
    /// chose. Nothing here depends on wall-clock time.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    /// <summary>
    /// Nothing held and no rate floor: the policy is out of the way, so each test below reads as a
    /// statement about the gate it names. The quota and the floor have their own tests.
    /// </summary>
    private static readonly RedemptionPolicy Unrestricted = new(
        QuotaUsed: 0,
        MinimumInterval: TimeSpan.Zero
    );

    [Fact]
    public void Constructor_sets_defaults()
    {
        var entry = CreateEntry();

        entry.NotifyOnAvailable.Should().BeTrue();
        entry.AutoRedeem.Should().BeFalse();
        entry.IsBlockedOnInput.Should().BeFalse();
        entry.RedemptionMessage.Should().BeNull();
    }

    [Fact]
    public void SetRedemptionMessage_throws_when_messages_not_allowed()
    {
        var entry = CreateEntry();

        var act = () => entry.SetRedemptionMessage("hello", allowMessages: false, Now);

        act.Should().Throw<InvalidOperationException>().WithMessage("*does not allow messages*");
    }

    [Fact]
    public void SetRedemptionMessage_succeeds_with_null_when_messages_not_allowed()
    {
        var entry = CreateEntry();

        var act = () => entry.SetRedemptionMessage(null, allowMessages: false, Now);

        act.Should().NotThrow();
    }

    [Fact]
    public void SetRedemptionMessage_stores_value_when_messages_allowed()
    {
        var entry = CreateEntry();

        entry.SetRedemptionMessage("hello", allowMessages: true, Now);

        entry.RedemptionMessage.Should().Be("hello");
    }

    [Fact]
    public void EnableAutoRedeem_sets_flag_and_clears_block()
    {
        var entry = CreateEntry();
        entry.BlockOnMissingInput(Now);

        entry.EnableAutoRedeem(Now);

        entry.AutoRedeem.Should().BeTrue();
        entry.IsBlockedOnInput.Should().BeFalse();
    }

    [Fact]
    public void DisableAutoRedeem_clears_flag()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);

        entry.DisableAutoRedeem(Now);

        entry.AutoRedeem.Should().BeFalse();
    }

    [Fact]
    public void BlockOnMissingInput_disables_auto_redeem_and_sets_block()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);

        entry.BlockOnMissingInput(Now);

        entry.AutoRedeem.Should().BeFalse();
        entry.IsBlockedOnInput.Should().BeTrue();
    }

    [Fact]
    public void ClearInputBlock_clears_flag_without_re_enabling()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        entry.BlockOnMissingInput(Now);

        entry.ClearInputBlock(Now);

        entry.IsBlockedOnInput.Should().BeFalse();
        entry.AutoRedeem.Should().BeFalse();
    }

    [Fact]
    public void SetNotifyOnAvailable_updates_value()
    {
        var entry = CreateEntry();

        entry.SetNotifyOnAvailable(false, Now);

        entry.NotifyOnAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData(nameof(WishlistEntry.SetNotifyOnAvailable))]
    [InlineData(nameof(WishlistEntry.SetRedemptionMessage))]
    [InlineData(nameof(WishlistEntry.EnableAutoRedeem))]
    [InlineData(nameof(WishlistEntry.DisableAutoRedeem))]
    [InlineData(nameof(WishlistEntry.BlockOnMissingInput))]
    [InlineData(nameof(WishlistEntry.ClearInputBlock))]
    public void Mutators_stamp_UpdatedAt_from_the_supplied_now(string mutator)
    {
        var entry = CreateEntry();
        var mutatedAt = Now.AddHours(7);

        Action apply = mutator switch
        {
            nameof(WishlistEntry.SetNotifyOnAvailable) => () =>
                entry.SetNotifyOnAvailable(false, mutatedAt),
            nameof(WishlistEntry.SetRedemptionMessage) => () =>
                entry.SetRedemptionMessage("hello", allowMessages: true, mutatedAt),
            nameof(WishlistEntry.EnableAutoRedeem) => () => entry.EnableAutoRedeem(mutatedAt),
            nameof(WishlistEntry.DisableAutoRedeem) => () => entry.DisableAutoRedeem(mutatedAt),
            nameof(WishlistEntry.BlockOnMissingInput) => () => entry.BlockOnMissingInput(mutatedAt),
            nameof(WishlistEntry.ClearInputBlock) => () => entry.ClearInputBlock(mutatedAt),
            _ => throw new ArgumentOutOfRangeException(nameof(mutator), mutator, null),
        };

        apply();

        entry.UpdatedAt.Should().Be(mutatedAt);
    }

    [Fact]
    public void CreateRedemption_snapshots_name_and_message()
    {
        var entry = CreateEntry();
        var storeItem = CreateStoreItem(entry.StoreItemId);

        var redemption = entry.CreateRedemption(storeItem, "msg", "Item", "Store", Now);

        redemption.StoreItemName.Should().Be("Item");
        redemption.StoreName.Should().Be("Store");
        redemption.MessageSnapshot.Should().Be("msg");
        redemption.Status.Should().Be(RedemptionStatus.Pending);
    }

    [Fact]
    public void CreateRedemption_stamps_AttemptedAt_from_the_supplied_now()
    {
        var entry = CreateEntry();
        var storeItem = CreateStoreItem(entry.StoreItemId);
        var attemptedAt = Now.AddDays(3);

        var redemption = entry.CreateRedemption(storeItem, null, "Item", "Store", attemptedAt);

        redemption.AttemptedAt.Should().Be(attemptedAt);
    }

    [Fact]
    public void CreateRedemption_sets_LastRedemptionCreatedAt_and_UpdatedAt()
    {
        var entry = CreateEntry();
        var storeItem = CreateStoreItem(entry.StoreItemId);
        var now = Now.AddMinutes(5);

        entry.CreateRedemption(storeItem, null, "Item", "Store", now);

        entry.LastRedemptionCreatedAt.Should().Be(now);
        entry.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void CanCreateRedemption_returns_false_when_AutoRedeem_off()
    {
        var entry = CreateEntry();
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1);

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_returns_false_when_item_unavailable()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: false, quantity: 0);

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_returns_true_when_never_redeemed()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1);

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_returns_false_when_within_cooldown()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(60_000, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now.AddSeconds(-30));

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_returns_true_when_cooldown_elapsed()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(60_000, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now.AddSeconds(-90));

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeTrue();
    }

    /// <summary>
    /// The cooldown window closes inclusively (<c>unblocksAt &lt;= now</c>), so the redemption is
    /// permitted on the exact millisecond the cooldown expires. Only an explicit clock can pin this.
    /// </summary>
    [Fact]
    public void CanCreateRedemption_returns_true_on_the_exact_cooldown_boundary()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(60_000, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now.AddMilliseconds(-60_000));

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_returns_false_one_millisecond_before_the_cooldown_boundary()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(60_000, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now.AddMilliseconds(-59_999));

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_uses_max_of_user_and_global_cooldown()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(10_000, 60_000, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now.AddSeconds(-30));

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_cooldown_uses_milliseconds()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(500, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now.AddMilliseconds(-1000));

        entry.CanCreateRedemption(item, Now, long.MaxValue, Unrestricted).Should().BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_returns_false_when_cost_exceeds_balance()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1); // Cost = 100

        entry.CanCreateRedemption(item, Now, availableBalance: 99, Unrestricted).Should().BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_returns_true_when_balance_equals_cost()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1); // Cost = 100

        entry.CanCreateRedemption(item, Now, availableBalance: 100, Unrestricted).Should().BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_returns_true_when_balance_exceeds_cost()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1); // Cost = 100

        entry.CanCreateRedemption(item, Now, availableBalance: 500, Unrestricted).Should().BeTrue();
    }

    [Fact]
    public void Constructor_defaults_to_a_single_desired_redemption()
    {
        CreateEntry().DesiredRedemptions.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(WishlistEntry.DesiredRedemptionsMax)]
    [InlineData(null)]
    public void SetDesiredRedemptions_accepts_a_count_in_range_and_unlimited(int? desired)
    {
        var entry = CreateEntry();

        entry.SetDesiredRedemptions(desired, Now);

        entry.DesiredRedemptions.Should().Be(desired);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WishlistEntry.DesiredRedemptionsMax + 1)]
    public void SetDesiredRedemptions_rejects_a_count_out_of_range(int desired)
    {
        var entry = CreateEntry();

        var act = () => entry.SetDesiredRedemptions(desired, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    public void HasQuotaRemaining_compares_against_the_desired_count(int quotaUsed, bool expected)
    {
        var entry = CreateEntry();
        entry.SetDesiredRedemptions(2, Now);

        entry.HasQuotaRemaining(quotaUsed).Should().Be(expected);
    }

    [Fact]
    public void HasQuotaRemaining_is_always_true_when_unlimited()
    {
        var entry = CreateEntry();
        entry.SetDesiredRedemptions(null, Now);

        entry.HasQuotaRemaining(int.MaxValue).Should().BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_returns_false_when_the_quota_is_met()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        entry.SetDesiredRedemptions(2, Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1);

        entry
            .CanCreateRedemption(
                item,
                Now,
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 2, MinimumInterval: TimeSpan.Zero)
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_returns_true_while_the_quota_has_room()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        entry.SetDesiredRedemptions(2, Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1);

        entry
            .CanCreateRedemption(
                item,
                Now,
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 1, MinimumInterval: TimeSpan.Zero)
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_ignores_the_quota_when_the_entry_is_unlimited()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        entry.SetDesiredRedemptions(null, Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1);

        entry
            .CanCreateRedemption(
                item,
                Now,
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 500, MinimumInterval: TimeSpan.Zero)
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_applies_the_minimum_interval_to_an_item_with_no_cooldown()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        entry.SetDesiredRedemptions(null, Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown(0, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now);

        entry
            .CanCreateRedemption(
                item,
                Now.AddMinutes(4),
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 0, MinimumInterval: TimeSpan.FromMinutes(5))
            )
            .Should()
            .BeFalse();

        entry
            .CanCreateRedemption(
                item,
                Now.AddMinutes(5),
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 0, MinimumInterval: TimeSpan.FromMinutes(5))
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CanCreateRedemption_keeps_a_cooldown_longer_than_the_minimum_interval()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        entry.SetDesiredRedemptions(null, Now);
        var item = CreateStoreItem(
            entry.StoreItemId,
            enabled: true,
            quantity: -1,
            cooldown: new ItemCooldown((long)TimeSpan.FromHours(1).TotalMilliseconds, 0, 0)
        );

        entry.CreateRedemption(item, null, "Item", "Store", Now);

        entry
            .CanCreateRedemption(
                item,
                Now.AddMinutes(30),
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 0, MinimumInterval: TimeSpan.FromMinutes(5))
            )
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanCreateRedemption_does_not_apply_the_minimum_interval_before_a_first_redemption()
    {
        var entry = CreateEntry();
        entry.EnableAutoRedeem(Now);
        var item = CreateStoreItem(entry.StoreItemId, enabled: true, quantity: -1);

        entry
            .CanCreateRedemption(
                item,
                Now,
                long.MaxValue,
                new RedemptionPolicy(QuotaUsed: 0, MinimumInterval: TimeSpan.FromHours(1))
            )
            .Should()
            .BeTrue();
    }

    private static WishlistEntry CreateEntry()
    {
        return new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now);
    }

    private static StoreItem CreateStoreItem(
        Guid id,
        bool enabled = true,
        int quantity = -1,
        ItemCooldown? cooldown = null
    )
    {
        var data = new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId("se-1"),
            new StoreExternalId("ch-1"),
            "Test Item",
            null,
            "perk",
            100,
            "default",
            enabled,
            false,
            true,
            false,
            false,
            [],
            [],
            new ItemQuantity(quantity == -1 ? -1 : Math.Max(quantity, 0), quantity),
            cooldown ?? new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );

        return new StoreItem(id, Guid.NewGuid(), data);
    }
}
