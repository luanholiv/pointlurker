using System.Reflection;
using CsCheck;
using FluentAssertions;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.ValueObjects;

/// <summary>
/// Characterization tests pinning the behaviour persistence relies on, captured before
/// <see cref="ItemQuantity" /> moves off its <c>jsonb</c> value converter onto an EF complex type.
/// Both mappings materialize through the same parameterized constructor, so these must stay green
/// across the change — that is what they are for.
/// </summary>
public sealed class ItemQuantityMaterializationTests
{
    private static readonly ConstructorInfo BindingConstructor =
        typeof(ItemQuantity).GetConstructor([typeof(int), typeof(int)])!;

    [Fact]
    public void The_parameterized_constructor_is_the_only_binding_path()
    {
        BindingConstructor
            .Should()
            .NotBeNull("EF complex-type binding and System.Text.Json both materialize through it");

        typeof(ItemQuantity)
            .GetConstructors()
            .Should()
            .ContainSingle("a second constructor would give persistence an ambiguous binding path");
    }

    [Fact]
    public void Materializing_through_the_binding_constructor_echoes_both_components()
    {
        Gen.Select(Gen.Int[-1, 1000], Gen.Int[-1, 1000])
            .Sample(
                (total, current) =>
                {
                    var quantity = (ItemQuantity)BindingConstructor.Invoke([total, current]);

                    quantity.Total.Should().Be(total);
                    quantity.Current.Should().Be(current);
                },
                iter: 1_000
            );
    }

    [Fact]
    public void Materializing_unlimited_stock_preserves_the_minus_one_sentinel()
    {
        var quantity = (ItemQuantity)BindingConstructor.Invoke([-1, -1]);

        quantity.Total.Should().Be(-1);
        quantity.Current.Should().Be(-1);
    }

    [Fact]
    public void Materializing_a_depleted_quantity_preserves_zero()
    {
        var quantity = (ItemQuantity)BindingConstructor.Invoke([10, 0]);

        quantity.Total.Should().Be(10);
        quantity.Current.Should().Be(0);
    }

    [Fact]
    public void The_invariant_is_enforced_on_the_materialization_path()
    {
        // A stored value below the -1 sentinel cannot be materialized silently: the guard runs
        // wherever persistence rebuilds the value, not only where application code constructs it.
        var act = () => BindingConstructor.Invoke([0, -2]);

        act.Should()
            .Throw<TargetInvocationException>()
            .WithInnerException<ArgumentOutOfRangeException>()
            .Which.ParamName.Should()
            .Be("current");
    }
}
