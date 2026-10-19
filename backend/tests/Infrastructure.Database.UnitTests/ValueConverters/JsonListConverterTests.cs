using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database.ValueConverters;

namespace PointLurker.Infrastructure.Database.UnitTests.ValueConverters;

/// <summary>
/// <see cref="JsonListConverter{T}"/> replaced three hand-written converters that differed only in
/// their type argument. The columns it maps already hold data, so the payload it writes must stay
/// byte-identical to what those three produced — that is what the exact-JSON assertions here pin.
/// A change in casing or in the shape of the empty list would leave existing <c>jsonb</c> rows
/// undeserializable, with no other test in the suite covering it.
/// </summary>
public sealed class JsonListConverterTests
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private static string ToProvider<T>(IReadOnlyList<T> list) =>
        (string)new JsonListConverter<T>().ConvertToProvider(list)!;

    private static IReadOnlyList<T> FromProvider<T>(string json) =>
        (IReadOnlyList<T>)new JsonListConverter<T>().ConvertFromProvider(json)!;

    [Fact]
    public void Serializes_strings_as_a_plain_json_array()
    {
        ToProvider<string>(["alpha", "beta"]).Should().Be("""["alpha","beta"]""");
    }

    [Fact]
    public void Serializes_StoreInputAlias_with_camelCase_property_names()
    {
        var aliases = new[] { new StoreInputAlias("Minecraft Username", "minecraft_username") };

        ToProvider<StoreInputAlias>(aliases)
            .Should()
            .Be("""[{"rawLabel":"Minecraft Username","labelCanonical":"minecraft_username"}]""");
    }

    [Fact]
    public void Serializes_StoreInputValue_with_camelCase_property_names()
    {
        var values = new[] { new StoreInputValue("minecraft_username", "ciphertext", UpdatedAt) };

        ToProvider<StoreInputValue>(values)
            .Should()
            .Be(
                """
                [{"labelCanonical":"minecraft_username","encryptedValue":"ciphertext","updatedAt":"2026-03-14T15:09:26+00:00"}]
                """.Trim()
            );
    }

    [Fact]
    public void Serializes_an_empty_list_as_an_empty_json_array()
    {
        // The jsonb columns declare HasDefaultValue(Array.Empty<T>()), which EF stores by pushing it
        // through this converter. The model snapshot records the result as "[]"; anything else here
        // would change the recorded default and demand a migration.
        ToProvider<StoreInputValue>([]).Should().Be("[]");
        ToProvider<StoreInputAlias>([]).Should().Be("[]");
        ToProvider<string>([]).Should().Be("[]");
    }

    [Fact]
    public void Round_trips_StoreInputValue()
    {
        IReadOnlyList<StoreInputValue> values =
        [
            new("minecraft_username", "cipher-1", UpdatedAt),
            new("discord_tag", "cipher-2", UpdatedAt.AddMinutes(5)),
        ];

        FromProvider<StoreInputValue>(ToProvider(values)).Should().BeEquivalentTo(values);
    }

    [Fact]
    public void Round_trips_StoreInputAlias()
    {
        IReadOnlyList<StoreInputAlias> aliases =
        [
            new("Minecraft Username", "minecraft_username"),
            new("Discord Tag", "discord_tag"),
        ];

        FromProvider<StoreInputAlias>(ToProvider(aliases)).Should().BeEquivalentTo(aliases);
    }

    [Fact]
    public void Round_trips_strings_preserving_order()
    {
        IReadOnlyList<string> definitions = ["Minecraft Username", "Discord Tag"];

        FromProvider<string>(ToProvider(definitions)).Should().Equal(definitions);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    public void Deserializes_empty_and_null_payloads_to_an_empty_list(string json)
    {
        // Rows written before the column existed read back as JSON null; the aggregate treats input
        // values as a collection that is always present, so null must not surface as one.
        FromProvider<StoreInputValue>(json).Should().BeEmpty();
    }

    [Fact]
    public void Deserializes_payloads_written_by_the_converter_it_replaced()
    {
        // Verbatim payloads as StoreInputValueListJsonConverter / StoreInputAliasListJsonConverter /
        // StringListJsonConverter wrote them, i.e. what is already sitting in the database.
        FromProvider<StoreInputValue>(
                """[{"labelCanonical":"minecraft_username","encryptedValue":"cipher","updatedAt":"2026-03-14T15:09:26+00:00"}]"""
            )
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(new StoreInputValue("minecraft_username", "cipher", UpdatedAt));

        FromProvider<StoreInputAlias>(
                """[{"rawLabel":"Minecraft Username","labelCanonical":"minecraft_username"}]"""
            )
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(new StoreInputAlias("Minecraft Username", "minecraft_username"));

        FromProvider<string>("""["Minecraft Username"]""").Should().Equal("Minecraft Username");
    }
}
