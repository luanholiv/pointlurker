using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Stores;

public sealed class StoreProviderSlugsTests
{
    [Theory]
    [InlineData(StoreProvider.StreamElements, "streamelements")]
    [InlineData(StoreProvider.GauPoints, "gaupoints")]
    public void A_slug_round_trips(StoreProvider provider, string slug)
    {
        provider.ToSlug().Should().Be(slug);

        StoreProviderSlugs.TryParse(slug, out var parsed).Should().BeTrue();
        parsed.Should().Be(provider);
    }

    /// <summary>
    /// The numeric value is a persistence detail that <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/>
    /// would happily accept, and a differently-cased name is a second address for the same resource.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("StreamElements")]
    [InlineData("STREAMELEMENTS")]
    [InlineData("stream-elements")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_but_the_exact_slug_is_refused(string? slug)
    {
        StoreProviderSlugs.TryParse(slug, out _).Should().BeFalse();
    }
}
