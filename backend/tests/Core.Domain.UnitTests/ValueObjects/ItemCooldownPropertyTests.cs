using System.Text.Json;
using Core.Domain.UnitTests.Generators;
using CsCheck;
using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ItemCooldownPropertyTests
{
    [Fact]
    public void In_range_construction_echoes_inputs()
    {
        Gen.Select(Gen.Long[0, 1_000_000], Gen.Long[0, 1_000_000], Gen.Long[0, 1_000_000])
            .Sample(
                (user, global, category) =>
                {
                    var cooldown = new ItemCooldown(user, global, category);
                    cooldown.UserMs.Should().Be(user);
                    cooldown.GlobalMs.Should().Be(global);
                    cooldown.CategoryMs.Should().Be(category);
                },
                iter: 10_000
            );
    }

    [Fact]
    public void Negative_user_ms_throws()
    {
        Gen.Select(Gen.Long[long.MinValue, -1], Gen.Long[0, 1_000_000], Gen.Long[0, 1_000_000])
            .Sample(
                (user, global, category) =>
                {
                    var act = () => new ItemCooldown(user, global, category);
                    act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("userMs");
                }
            );
    }

    [Fact]
    public void Negative_global_ms_throws()
    {
        Gen.Select(Gen.Long[0, 1_000_000], Gen.Long[long.MinValue, -1], Gen.Long[0, 1_000_000])
            .Sample(
                (user, global, category) =>
                {
                    var act = () => new ItemCooldown(user, global, category);
                    act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("globalMs");
                }
            );
    }

    [Fact]
    public void Negative_category_ms_throws()
    {
        Gen.Select(Gen.Long[0, 1_000_000], Gen.Long[0, 1_000_000], Gen.Long[long.MinValue, -1])
            .Sample(
                (user, global, category) =>
                {
                    var act = () => new ItemCooldown(user, global, category);
                    act.Should()
                        .Throw<ArgumentOutOfRangeException>()
                        .WithParameterName("categoryMs");
                }
            );
    }

    [Fact]
    public void Valid_value_round_trips_through_json()
    {
        DomainGen.Cooldowns.Sample(cooldown =>
        {
            var json = JsonSerializer.Serialize(cooldown);
            var roundTripped = JsonSerializer.Deserialize<ItemCooldown>(json);
            roundTripped.Should().Be(cooldown);
        });
    }
}
