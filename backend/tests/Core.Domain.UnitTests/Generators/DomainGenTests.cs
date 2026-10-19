using CsCheck;
using FluentAssertions;

namespace Core.Domain.UnitTests.Generators;

public sealed class DomainGenTests
{
    [Fact]
    public void Quantities_only_yield_in_range_values()
    {
        DomainGen.Quantities.Sample(q =>
        {
            q.Total.Should().BeGreaterThanOrEqualTo(-1);
            q.Current.Should().BeGreaterThanOrEqualTo(-1);
        });
    }

    [Fact]
    public void Cooldowns_only_yield_non_negative_values()
    {
        DomainGen.Cooldowns.Sample(c =>
        {
            c.UserMs.Should().BeGreaterThanOrEqualTo(0);
            c.GlobalMs.Should().BeGreaterThanOrEqualTo(0);
            c.CategoryMs.Should().BeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public void ApiData_constructs_without_throwing()
    {
        DomainGen.ApiData.Sample(data => data.Quantity.Current >= -1);
    }

    [Fact]
    public void Sequences_stay_within_bounded_length()
    {
        DomainGen.Sequences.Sample(seq => seq.Count is >= 0 and <= 16);
    }
}
