using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ChannelIdTests
{
    [Fact]
    public void Constructor_stores_value()
    {
        var id = new StoreExternalId("xqc-channel");
        id.Value.Should().Be("xqc-channel");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Constructor_rejects_empty_or_whitespace(string? value)
    {
        var act = () => new StoreExternalId(value!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToString_returns_value()
    {
        var id = new StoreExternalId("my-channel");
        id.ToString().Should().Be("my-channel");
    }

    [Fact]
    public void Equality_holds_for_same_value()
    {
        var a = new StoreExternalId("ch-1");
        var b = new StoreExternalId("ch-1");
        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Inequality_holds_for_different_value()
    {
        var a = new StoreExternalId("ch-1");
        var b = new StoreExternalId("ch-2");
        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_equal_for_same_value()
    {
        var a = new StoreExternalId("ch-1");
        var b = new StoreExternalId("ch-1");
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
