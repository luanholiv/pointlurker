using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;

namespace Core.Application.UnitTests;

public class CoreApplicationServiceCollectionExtensionsTests
{
    /// <summary>
    /// Every use case the composition root must be able to hand to an endpoint or a background
    /// job. Kept explicit: an assembly scan cannot tell <see cref="AddWatchedStore"/> apart from
    /// the DTOs, errors and options validators that share the same namespaces.
    /// </summary>
    private static readonly Type[] UseCases =
    [
        typeof(SetProviderCredential),
        typeof(ClearProviderCredential),
        typeof(SetUserLocale),
        typeof(RefreshProviderAccounts),
        typeof(AddWatchedStore),
        typeof(ListWatchedStores),
        typeof(GetStoreDetail),
        typeof(ListStoreItems),
        typeof(GetStoreItemFacets),
        typeof(ActivateStore),
        typeof(DeactivateStore),
        typeof(RemoveStore),
        typeof(AddWishlistEntry),
        typeof(UpdateWishlistEntry),
        typeof(RemoveWishlistEntry),
        typeof(GetWishlistEntry),
        typeof(ListWishlistEntriesGrouped),
        typeof(GetStoreInputProfile),
        typeof(UpsertStoreProfileValues),
        typeof(UpsertStoreProfileAlias),
        typeof(DeleteStoreProfileValue),
        typeof(DeleteStoreProfileAlias),
        typeof(SyncStores),
        typeof(SubmitRedemptions),
        typeof(PollRedemptionStatus),
        typeof(ListRedemptions),
    ];

    [Fact]
    public void Registers_every_use_case_exactly_once()
    {
        var services = new ServiceCollection().AddCoreApplication();

        var notRegisteredExactlyOnce = UseCases
            .Where(t => services.Count(d => d.ServiceType == t) != 1)
            .Select(t => t.Name)
            .ToArray();

        notRegisteredExactlyOnce.Should().BeEmpty();
    }

    [Fact]
    public void Registers_every_use_case_as_scoped()
    {
        var services = new ServiceCollection().AddCoreApplication();

        var wrongLifetime = UseCases
            .Select(t => new
            {
                t.Name,
                Descriptor = services.FirstOrDefault(d => d.ServiceType == t),
            })
            .Where(x => x.Descriptor?.Lifetime != ServiceLifetime.Scoped)
            .Select(x => x.Name)
            .ToArray();

        wrongLifetime.Should().BeEmpty();
    }

    /// <summary>
    /// The single clock for the whole backend: use cases, hosted services and endpoint handlers all
    /// resolve this one registration rather than reading <c>DateTimeOffset.UtcNow</c> themselves.
    /// </summary>
    [Fact]
    public void Registers_the_system_clock_as_a_single_singleton_TimeProvider()
    {
        var services = new ServiceCollection().AddCoreApplication();

        var descriptor = services.Single(d => d.ServiceType == typeof(TimeProvider));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        descriptor.ImplementationInstance.Should().BeSameAs(TimeProvider.System);
    }

    /// <summary>
    /// Registration is <c>TryAdd</c>-based so a host (or a test) can substitute a clock before
    /// calling <c>AddCoreApplication</c> without ending up with two registrations.
    /// </summary>
    [Fact]
    public void Does_not_replace_a_TimeProvider_the_host_already_registered()
    {
        var custom = new FakeTimeProvider();

        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(custom);
        services.AddCoreApplication();

        var descriptor = services.Single(d => d.ServiceType == typeof(TimeProvider));

        descriptor.ImplementationInstance.Should().BeSameAs(custom);
    }

    [Fact]
    public void Registers_the_fuzzy_input_label_resolver_as_a_singleton()
    {
        var services = new ServiceCollection().AddCoreApplication();

        var descriptor = services.Single(d => d.ServiceType == typeof(IInputLabelResolver));

        descriptor.ImplementationType.Should().Be(typeof(FuzzyInputLabelResolver));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Shared by auto-redeem and submission, and stateless apart from the two singletons it
    /// composes, so it is registered once for the whole host rather than per scope.
    /// </summary>
    [Fact]
    public void Registers_the_store_input_value_resolver_as_a_singleton()
    {
        var services = new ServiceCollection().AddCoreApplication();

        var descriptor = services.Single(d => d.ServiceType == typeof(IStoreInputValueResolver));

        descriptor.ImplementationType.Should().Be(typeof(StoreInputValueResolver));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// The two units a sync cycle runs per store. They are scoped like any other use case, but the
    /// coordinator reaches them through a factory rather than a plain constructor parameter, because
    /// stores are processed in parallel and each one needs a scope — a DbContext — of its own.
    /// </summary>
    private static readonly Type[] PerStoreSyncUnits =
    [
        typeof(StoreSyncUnit),
        typeof(StoreInputCoverageUnit),
    ];

    [Theory]
    [MemberData(nameof(PerStoreSyncUnitTypes))]
    public void Registers_each_per_store_sync_unit_as_scoped(Type unit)
    {
        var services = new ServiceCollection().AddCoreApplication();

        var descriptor = services.Single(d => d.ServiceType == unit);

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Theory]
    [MemberData(nameof(PerStoreSyncUnitTypes))]
    public void Registers_a_singleton_factory_for_each_per_store_sync_unit(Type unit)
    {
        var services = new ServiceCollection().AddCoreApplication();

        var factoryType = typeof(Func<>).MakeGenericType(
            typeof(ScopedInstance<>).MakeGenericType(unit)
        );

        var descriptor = services.Single(d => d.ServiceType == factoryType);

        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    public static TheoryData<Type> PerStoreSyncUnitTypes() => [.. PerStoreSyncUnits];

    /// <summary>
    /// Each call opens a scope of its own, so two units taken from the factory never share the
    /// scoped repositories underneath — which is what makes the parallel per-store pass safe.
    /// </summary>
    [Fact]
    public async Task Resolves_a_fresh_per_store_unit_on_every_factory_call()
    {
        var services = new ServiceCollection().AddCoreApplication();
        services.AddScoped(_ => Substitute.For<IStoreItemRepository>());
        services.AddScoped(_ => Substitute.For<IWatchedStoreRepository>());
        services.AddScoped(_ => Substitute.For<IStoreProviderClientFactory>());
        services.AddScoped(_ => Substitute.For<IUnitOfWork>());
        services.AddSingleton<ILogger<StoreSyncUnit>>(NullLogger<StoreSyncUnit>.Instance);
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<Func<ScopedInstance<StoreSyncUnit>>>();

        await using var first = factory();
        await using var second = factory();

        first.Value.Should().NotBeSameAs(second.Value);
    }

    [Theory]
    [InlineData(typeof(SyncSettings), typeof(SyncSettingsValidator))]
    [InlineData(typeof(RedemptionsOptions), typeof(RedemptionsOptionsValidator))]
    [InlineData(typeof(InputResolutionOptions), typeof(InputResolutionOptionsValidator))]
    public void Registers_a_startup_validated_validator_for_each_options_type(
        Type options,
        Type validator
    )
    {
        var services = new ServiceCollection().AddCoreApplication();

        var validatorInterface = typeof(IValidateOptions<>).MakeGenericType(options);

        services
            .Should()
            .Contain(d => d.ServiceType == validatorInterface && d.ImplementationType == validator);
    }

    [Fact]
    public void Binds_its_own_options_from_configuration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Sync:FetchCronExpression"] = "*/10 * * * *",
                        ["Sync:UsernameRefreshCronExpression"] = "0 4 * * *",
                        ["Sync:MaxDegreeOfParallelism"] = "3",
                        ["Redemptions:PollingCronExpression"] = "*/2 * * * *",
                        ["Redemptions:CompletionTimeoutHours"] = "6",
                        ["InputResolution:MatchThreshold"] = "70",
                    }
                )
                .Build()
        );
        services.AddCoreApplication();

        var provider = services.BuildServiceProvider();

        provider
            .GetRequiredService<IOptions<SyncSettings>>()
            .Value.FetchCronExpression.Should()
            .Be("*/10 * * * *");
        provider
            .GetRequiredService<IOptions<RedemptionsOptions>>()
            .Value.CompletionTimeoutHours.Should()
            .Be(6);
        provider
            .GetRequiredService<IOptions<InputResolutionOptions>>()
            .Value.MatchThreshold.Should()
            .Be(70);
    }
}
