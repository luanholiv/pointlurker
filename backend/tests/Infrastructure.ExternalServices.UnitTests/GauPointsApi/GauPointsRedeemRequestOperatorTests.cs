using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.ExternalServices.GauPointsApi;

namespace Infrastructure.ExternalServices.UnitTests.GauPointsApi;

public sealed class GauPointsRedeemRequestOperatorTests
{
    private static ProviderRedeemCommand Command(params ProviderInputValue[] input) =>
        new(
            Guid.NewGuid(),
            new StoreExternalId("gaupoints"),
            new ProviderItemId("netflix"),
            input,
            null
        );

    [Fact]
    public void Each_value_travels_under_the_wire_key_of_its_label()
    {
        var request = (GauPointsRedeemRequest)Command(
            new("Full name", "Ana Souza"),
            new("Phone", "+5511999999999"),
            new("E-mail", "ana@example.com"),
            new("Address", "Rua A, 1")
        );

        var json = JsonNode.Parse(JsonSerializer.Serialize(request))!;
        json["itemId"]!.GetValue<string>().Should().Be("netflix");
        json["data"]!
            .AsObject()
            .Select(pair => (pair.Key, pair.Value!.GetValue<string>()))
            .Should()
            .Equal(
                ("fullName", "Ana Souza"),
                ("phone", "+5511999999999"),
                ("email", "ana@example.com"),
                ("address", "Rua A, 1")
            );
    }

    /// <summary>The site's own form always posts all four keys; a three-key body is untested there.</summary>
    [Fact]
    public void A_digital_redemption_still_sends_the_address_key_empty()
    {
        var request = (GauPointsRedeemRequest)Command(
            new("Full name", "Ana Souza"),
            new("Phone", "+5511999999999"),
            new("E-mail", "ana@example.com")
        );

        JsonSerializer.Serialize(request).Should().Contain("\"address\":\"\"");
    }
}
