using CsCheck;
using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

public sealed class ItemAvailabilityPropertyTests
{
    // Range spans the -1 unlimited sentinel and the current == 0 boundary.
    [Fact]
    public void Availability_matches_the_truth_table()
    {
        Gen.Select(Gen.Bool, Gen.Int[-1, 1000], Gen.Int[-1, 1000])
            .Sample(
                (enabled, total, current) =>
                {
                    var availability = ItemAvailability.From(
                        enabled,
                        new ItemQuantity(total, current)
                    );

                    availability.IsAvailable.Should().Be(enabled && (current == -1 || current > 0));
                },
                iter: 10_000
            );
    }
}
