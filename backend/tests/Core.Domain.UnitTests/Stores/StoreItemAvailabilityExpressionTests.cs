using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

/// <summary>
/// The availability rule has to hold identically in memory and in SQL, so it is single-sourced as
/// <see cref="StoreItem.IsAvailableExpression" /> and composed into queries rather than restated.
/// This truth table is what stops the expression and <see cref="ItemAvailability.From" /> drifting:
/// if either definition moves without the other, one of these cases fails.
/// </summary>
public sealed class StoreItemAvailabilityExpressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    public static TheoryData<bool, int> TruthTable =>
        new()
        {
            { true, -1 },
            { true, 0 },
            { true, 1 },
            { false, -1 },
            { false, 0 },
            { false, 1 },
        };

    [Theory]
    [MemberData(nameof(TruthTable))]
    public void Compiled_expression_agrees_with_the_in_memory_invariant(
        bool enabled,
        int quantityCurrent
    )
    {
        var predicate = StoreItem.IsAvailableExpression.Compile();
        var item = CreateItem(enabled, quantityCurrent);

        predicate(item).Should().Be(item.Availability.IsAvailable);
    }

    [Theory]
    [MemberData(nameof(TruthTable))]
    public void Compiled_expression_agrees_with_ItemAvailability_From(
        bool enabled,
        int quantityCurrent
    )
    {
        var predicate = StoreItem.IsAvailableExpression.Compile();
        var quantity = new ItemQuantity(1000, quantityCurrent);

        predicate(CreateItem(enabled, quantityCurrent))
            .Should()
            .Be(ItemAvailability.From(enabled, quantity).IsAvailable);
    }

    [Fact]
    public void Unlimited_stock_is_available()
    {
        var predicate = StoreItem.IsAvailableExpression.Compile();

        predicate(CreateItem(enabled: true, quantityCurrent: -1)).Should().BeTrue();
    }

    [Fact]
    public void Depleted_stock_is_unavailable()
    {
        var predicate = StoreItem.IsAvailableExpression.Compile();

        predicate(CreateItem(enabled: true, quantityCurrent: 0)).Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void A_disabled_item_is_never_available(int quantityCurrent)
    {
        var predicate = StoreItem.IsAvailableExpression.Compile();

        predicate(CreateItem(enabled: false, quantityCurrent)).Should().BeFalse();
    }

    private static StoreItem CreateItem(bool enabled, int quantityCurrent) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-avail"),
                new StoreExternalId("ch-avail"),
                "Item",
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
                new ItemQuantity(1000, quantityCurrent),
                new ItemCooldown(0, 0, 0),
                Now,
                null,
                Now
            )
        );
}
