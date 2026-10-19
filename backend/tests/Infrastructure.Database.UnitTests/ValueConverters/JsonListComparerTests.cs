using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database.ValueConverters;

namespace PointLurker.Infrastructure.Database.UnitTests.ValueConverters;

/// <summary>
/// The jsonb list columns are mapped from mutable backing fields, so EF can only notice an edit by
/// comparing the collection against a snapshot it took when the entity was loaded. Now that writes
/// persist deltas rather than whole rows, these three behaviours are what stands between
/// <c>UpsertValue</c> / <c>UpsertAlias</c> / <c>UpdateFromApi</c> and a silently dropped change.
/// </summary>
public sealed class JsonListComparerTests
{
    private static readonly DateTimeOffset UpdatedAt = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    [Fact]
    public void Equal_when_contents_are_equal_in_order()
    {
        var comparer = JsonListComparer.Create<StoreInputAlias>();

        IReadOnlyList<StoreInputAlias> left = [new("Raw", "canonical")];
        IReadOnlyList<StoreInputAlias> right = [new("Raw", "canonical")];

        comparer.Equals(left, right).Should().BeTrue();
        comparer.GetHashCode(left).Should().Be(comparer.GetHashCode(right));
    }

    [Fact]
    public void Not_equal_when_an_element_differs()
    {
        var comparer = JsonListComparer.Create<StoreInputValue>();

        IReadOnlyList<StoreInputValue> before = [new("label", "cipher-1", UpdatedAt)];
        IReadOnlyList<StoreInputValue> after = [new("label", "cipher-2", UpdatedAt)];

        comparer.Equals(before, after).Should().BeFalse();
    }

    [Fact]
    public void Not_equal_when_order_differs()
    {
        var comparer = JsonListComparer.Create<string>();

        comparer.Equals(["a", "b"], ["b", "a"]).Should().BeFalse();
    }

    [Fact]
    public void Not_equal_when_an_element_is_added()
    {
        var comparer = JsonListComparer.Create<string>();

        comparer.Equals(["a"], ["a", "b"]).Should().BeFalse();
    }

    [Fact]
    public void Treats_null_as_an_empty_list()
    {
        var comparer = JsonListComparer.Create<string>();

        comparer.Equals(null, []).Should().BeTrue();
        comparer.Equals([], null).Should().BeTrue();
        comparer.GetHashCode(null!).Should().Be(0);
    }

    [Fact]
    public void Snapshot_is_independent_of_later_mutation()
    {
        // The decisive one: EF snapshots on load and diffs on save. If the snapshot aliased the live
        // list, every in-place edit would compare equal to itself and never be written.
        var comparer = JsonListComparer.Create<string>();
        var live = new List<string> { "a" };

        var snapshot = comparer.Snapshot(live);
        live.Add("b");

        snapshot.Should().Equal("a");
        comparer.Equals(snapshot, live).Should().BeFalse();
    }
}
