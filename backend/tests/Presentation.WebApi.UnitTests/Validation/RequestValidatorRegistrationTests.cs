using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Presentation.WebApi.Validation;

namespace Presentation.WebApi.UnitTests.Validation;

/// <summary>
/// The completeness guard the MVC hook never had. A validator that exists but is unregistered is
/// the same defect as a mapper wired to a hook that cannot fire: the code reads as covered and
/// nothing runs. Driven by reflection rather than a hand-kept list, so a new request contract
/// fails the build until it has a validator in the container.
/// </summary>
public class RequestValidatorRegistrationTests
{
    public static TheoryData<Type> RequestContracts()
    {
        var data = new TheoryData<Type>();

        foreach (var contract in DiscoverRequestContracts())
        {
            data.Add(contract);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RequestContracts))]
    public void Resolves_a_validator_for_every_request_contract(Type contract)
    {
        using var provider = new ServiceCollection().AddRequestValidation().BuildServiceProvider();

        var validator = provider.GetService(typeof(IValidator<>).MakeGenericType(contract));

        validator.Should().NotBeNull();
    }

    /// <summary>
    /// Guards the discovery itself: an empty set would make the theory above pass vacuously.
    /// </summary>
    [Fact]
    public void Finds_the_request_contracts_to_check()
    {
        DiscoverRequestContracts().Should().HaveCountGreaterThan(5);
    }

    private static IReadOnlyList<Type> DiscoverRequestContracts() =>
        typeof(ValidationEndpointFilter<>)
            .Assembly.GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null
                && type.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) == true
                && type.Name.EndsWith("Request", StringComparison.Ordinal)
            )
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
}
