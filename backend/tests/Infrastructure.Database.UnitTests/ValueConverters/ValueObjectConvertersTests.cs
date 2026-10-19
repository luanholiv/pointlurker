using System.Text.Json;
using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.Database.UnitTests.ValueConverters;

public class ValueObjectConvertersTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ItemQuantity is persisted as an EF complex type, not JSON — these pin the value object's
    // System.Text.Json behaviour, which its [JsonConstructor] still declares.
    [Fact]
    public void ItemQuantity_RoundTrips_NonZeroValues()
    {
        var original = new ItemQuantity(500, 309);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ItemQuantity>(json, JsonOptions);

        deserialized.Total.Should().Be(500);
        deserialized.Current.Should().Be(309);
    }

    [Fact]
    public void ItemQuantity_RoundTrips_UnlimitedStock()
    {
        var original = new ItemQuantity(-1, -1);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ItemQuantity>(json, JsonOptions);

        deserialized.Total.Should().Be(-1);
        deserialized.Current.Should().Be(-1);
    }

    [Fact]
    public void ItemQuantity_Deserializes_KnownJson()
    {
        var json = """{"total":500,"current":309}""";

        var deserialized = JsonSerializer.Deserialize<ItemQuantity>(json, JsonOptions);

        deserialized.Total.Should().Be(500);
        deserialized.Current.Should().Be(309);
    }

    [Fact]
    public void ItemCooldownConverter_RoundTrips_NonZeroValues()
    {
        var original = new ItemCooldown(3600000, 60000, 0);

        var json = JsonSerializer.Serialize(original, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<ItemCooldown>(json, JsonOptions);

        deserialized.UserMs.Should().Be(3600000);
        deserialized.GlobalMs.Should().Be(60000);
        deserialized.CategoryMs.Should().Be(0);
    }

    [Fact]
    public void ItemCooldownConverter_Deserializes_KnownJson()
    {
        var json = """{"userMs":3600000,"globalMs":60000,"categoryMs":0}""";

        var deserialized = JsonSerializer.Deserialize<ItemCooldown>(json, JsonOptions);

        deserialized.UserMs.Should().Be(3600000);
        deserialized.GlobalMs.Should().Be(60000);
        deserialized.CategoryMs.Should().Be(0);
    }
}
