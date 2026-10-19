using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ItemCooldownTests
{
    [Fact]
    public void Constructor_stores_values()
    {
        var cd = new ItemCooldown(1000, 5000, 2000);
        cd.UserMs.Should().Be(1000);
        cd.GlobalMs.Should().Be(5000);
        cd.CategoryMs.Should().Be(2000);
    }

    [Fact]
    public void Constructor_accepts_zero()
    {
        var cd = new ItemCooldown(0, 0, 0);
        cd.UserMs.Should().Be(0);
        cd.GlobalMs.Should().Be(0);
        cd.CategoryMs.Should().Be(0);
    }

    [Fact]
    public void Constructor_rejects_negative_userMs()
    {
        var act = () => new ItemCooldown(-1, 0, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("userMs");
    }

    [Fact]
    public void Constructor_rejects_negative_globalMs()
    {
        var act = () => new ItemCooldown(0, -1, 0);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("globalMs");
    }

    [Fact]
    public void Constructor_rejects_negative_categoryMs()
    {
        var act = () => new ItemCooldown(0, 0, -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("categoryMs");
    }

    [Fact]
    public void Equality_holds_for_same_values()
    {
        var a = new ItemCooldown(1000, 2000, 3000);
        var b = new ItemCooldown(1000, 2000, 3000);
        a.Should().Be(b);
    }

    [Fact]
    public void Inequality_holds_for_different_values()
    {
        var a = new ItemCooldown(1000, 2000, 3000);
        var b = new ItemCooldown(1000, 2000, 9999);
        a.Should().NotBe(b);
    }
}
