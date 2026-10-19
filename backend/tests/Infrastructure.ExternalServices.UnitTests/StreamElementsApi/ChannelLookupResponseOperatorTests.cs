using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

namespace Infrastructure.ExternalServices.UnitTests.StreamElementsApi;

public sealed class ChannelLookupResponseOperatorTests
{
    [Fact]
    public void Maps_id_username_display_name_and_avatar()
    {
        var dto = new ChannelLookupResponse("channel-id-123", "xqc", "xQc")
        {
            Avatar = "https://cdn.example.com/avatar.png",
        };

        var lookup = (ProviderStoreLookup)dto;

        lookup
            .Should()
            .BeEquivalentTo(
                new ProviderStoreLookup(
                    new StoreExternalId("channel-id-123"),
                    "xqc",
                    "xQc",
                    "https://cdn.example.com/avatar.png"
                )
            );
    }

    [Fact]
    public void Maps_missing_avatar_to_null()
    {
        var dto = new ChannelLookupResponse("channel-id-123", "xqc", "xQc");

        var lookup = (ProviderStoreLookup)dto;

        lookup.AvatarUrl.Should().BeNull();
    }
}
