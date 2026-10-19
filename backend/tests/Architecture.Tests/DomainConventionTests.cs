using NetArchTest.Rules;
using PointLurker.Core.Domain.Common;

namespace PointLurker.Architecture.Tests;

public class DomainConventionTests
{
    [Fact]
    public void Repository_interfaces_only_in_Domain()
    {
        var nonDomain = ArchitectureAssemblies
            .SourceAssemblies.Where(a => a != ArchitectureAssemblies.Domain)
            .ToArray();

        var leaks = Types
            .InAssemblies(nonDomain)
            .That()
            .AreInterfaces()
            .And()
            .HaveNameStartingWith("I")
            .And()
            .HaveNameEndingWith("Repository")
            .GetTypes()
            .Select(t => t.FullName ?? t.Name)
            .ToArray();

        Assert.True(
            leaks.Length == 0,
            $"{nameof(Repository_interfaces_only_in_Domain)}: {string.Join(", ", leaks)}"
        );
    }

    [Fact]
    public void Domain_event_types_implement_IDomainEvent_and_are_sealed()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Domain)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(
                nameof(Domain_event_types_implement_IDomainEvent_and_are_sealed),
                result
            )
        );
    }

    [Fact]
    public void ValueObjects_are_sealed()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Domain)
            .That()
            .ResideInNamespace("PointLurker.Core.Domain.ValueObjects")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(nameof(ValueObjects_are_sealed), result)
        );
    }
}
