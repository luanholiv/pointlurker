using System.Text.Json;
using Core.Domain.UnitTests.Generators;
using CsCheck;
using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ItemQuantityPropertyTests
{
    [Fact]
    public void In_range_construction_echoes_inputs()
    {
        Gen.Select(Gen.Int[-1, 1000], Gen.Int[-1, 1000])
            .Sample(
                (total, current) =>
                {
                    var quantity = new ItemQuantity(total, current);
                    quantity.Total.Should().Be(total);
                    quantity.Current.Should().Be(current);
                },
                iter: 10_000
            );
    }

    [Fact]
    public void Total_below_minus_one_throws()
    {
        Gen.Select(Gen.Int[int.MinValue, -2], Gen.Int[-1, 1000])
            .Sample(
                (total, current) =>
                {
                    var act = () => new ItemQuantity(total, current);
                    act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("total");
                }
            );
    }

    [Fact]
    public void Current_below_minus_one_throws()
    {
        Gen.Select(Gen.Int[-1, 1000], Gen.Int[int.MinValue, -2])
            .Sample(
                (total, current) =>
                {
                    var act = () => new ItemQuantity(total, current);
                    act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("current");
                }
            );
    }

    [Fact]
    public void Valid_value_round_trips_through_json()
    {
        DomainGen.Quantities.Sample(quantity =>
        {
            var json = JsonSerializer.Serialize(quantity);
            var roundTripped = JsonSerializer.Deserialize<ItemQuantity>(json);
            roundTripped.Should().Be(quantity);
        });
    }
}
