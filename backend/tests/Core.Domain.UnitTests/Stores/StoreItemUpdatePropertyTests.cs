using Core.Domain.UnitTests.Generators;
using CsCheck;
using FluentAssertions;
using PointLurker.Core.Domain.Common;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class StoreItemUpdatePropertyTests
{
    [Fact]
    public void Each_update_emits_exactly_the_event_for_its_transition()
    {
        Gen.Select(DomainGen.Snapshots, DomainGen.Sequences)
            .Sample(
                (initial, rest) =>
                {
                    var item = new StoreItem(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        DomainGen.ToApiData(initial)
                    );

                    // The constructor seeds the previous availability as unavailable (false).
                    var previous = IsAvailable(initial);
                    AssertTransitionEvents(item.DomainEvents, before: false, after: previous);
                    item.ClearDomainEvents();

                    foreach (var snapshot in rest)
                    {
                        var next = IsAvailable(snapshot);
                        item.UpdateFromApi(DomainGen.ToApiData(snapshot));

                        AssertTransitionEvents(item.DomainEvents, before: previous, after: next);

                        item.ClearDomainEvents();
                        previous = next;
                    }
                }
            );
    }

    [Fact]
    public void Total_events_equal_the_availability_flip_count()
    {
        Gen.Select(DomainGen.Snapshots, DomainGen.Sequences)
            .Sample(
                (initial, rest) =>
                {
                    var item = new StoreItem(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        DomainGen.ToApiData(initial)
                    );

                    var totalEvents = item.DomainEvents.Count;
                    foreach (var snapshot in rest)
                    {
                        item.ClearDomainEvents();
                        item.UpdateFromApi(DomainGen.ToApiData(snapshot));
                        totalEvents += item.DomainEvents.Count;
                    }

                    // The availability path starts at the unavailable seed the constructor assumes.
                    var path = new List<bool> { false, IsAvailable(initial) };
                    path.AddRange(rest.Select(IsAvailable));

                    var flips = 0;
                    for (var i = 1; i < path.Count; i++)
                    {
                        if (path[i] != path[i - 1])
                        {
                            flips++;
                        }
                    }

                    totalEvents.Should().Be(flips);
                }
            );
    }

    private static bool IsAvailable((bool Enabled, ItemQuantity Quantity) snapshot) =>
        ItemAvailability.From(snapshot.Enabled, snapshot.Quantity).IsAvailable;

    private static void AssertTransitionEvents(
        IReadOnlyCollection<IDomainEvent> events,
        bool before,
        bool after
    )
    {
        if (!before && after)
        {
            events.Should().ContainSingle().Which.Should().BeOfType<StoreItemBecameAvailable>();
        }
        else if (before && !after)
        {
            events.Should().ContainSingle().Which.Should().BeOfType<StoreItemBecameUnavailable>();
        }
        else
        {
            events.Should().BeEmpty();
        }
    }
}
