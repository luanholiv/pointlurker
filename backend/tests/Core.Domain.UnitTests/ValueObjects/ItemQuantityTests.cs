using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ItemQuantityTests
{
    [Fact]
    public void Constructor_stores_values()
    {
        var q = new ItemQuantity(100, 50);
        q.Total.Should().Be(100);
        q.Current.Should().Be(50);
    }

    [Fact]
    public void Sentinel_minus_one_is_accepted_as_unlimited()
    {
        var q = new ItemQuantity(-1, -1);
        q.Total.Should().Be(-1);
        q.Current.Should().Be(-1);
    }

    [Fact]
    public void Constructor_rejects_total_below_minus_one()
    {
        var act = () => new ItemQuantity(-2, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("total");
    }

    [Fact]
    public void Constructor_rejects_current_below_minus_one()
    {
        var act = () => new ItemQuantity(0, -2);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("current");
    }

    [Fact]
    public void Zero_quantity_is_valid()
    {
        var q = new ItemQuantity(0, 0);
        q.Total.Should().Be(0);
        q.Current.Should().Be(0);
    }

    [Fact]
    public void Equality_holds_for_same_values()
    {
        var a = new ItemQuantity(10, 5);
        var b = new ItemQuantity(10, 5);
        a.Should().Be(b);
    }

    [Fact]
    public void Inequality_holds_for_different_values()
    {
        var a = new ItemQuantity(10, 5);
        var b = new ItemQuantity(10, 4);
        a.Should().NotBe(b);
    }
}
