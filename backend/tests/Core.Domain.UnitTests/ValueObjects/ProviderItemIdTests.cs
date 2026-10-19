using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ProviderItemIdTests
{
    [Fact]
    public void Constructor_stores_value()
    {
        var id = new ProviderItemId("se-item-abc");
        id.Value.Should().Be("se-item-abc");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Constructor_rejects_empty_or_whitespace(string? value)
    {
        var act = () => new ProviderItemId(value!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_returns_value()
    {
        var id = new ProviderItemId("item-xyz");
        id.ToString().Should().Be("item-xyz");
    }

    [Fact]
    public void Equality_holds_for_same_value()
    {
        var a = new ProviderItemId("se-1");
        var b = new ProviderItemId("se-1");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_holds_for_different_value()
    {
        var a = new ProviderItemId("se-1");
        var b = new ProviderItemId("se-2");
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_equal_for_same_value()
    {
        var a = new ProviderItemId("se-1");
        var b = new ProviderItemId("se-1");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
