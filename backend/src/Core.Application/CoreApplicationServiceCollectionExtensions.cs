using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Core.Application;

public static class CoreApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers every application use case plus the options this layer owns. Use cases are
    /// listed explicitly rather than discovered by assembly scan: the same namespaces also hold
    /// DTOs, errors, results and options validators, none of which belong in the container.
    /// </summary>
    public static IServiceCollection AddCoreApplication(this IServiceCollection services)
    {
        // The single clock for the whole backend. Everything that needs "now" — use cases, hosted
        // services, endpoint handlers — takes this rather than reading DateTimeOffset.UtcNow, so
        // tests can substitute a FakeTimeProvider instead of sleeping real wall-clock time.
        services.TryAddSingleton(TimeProvider.System);

        services.AddIdentityUseCases();
        services.AddStoreUseCases();

        services.AddSingleton<IInputLabelResolver, FuzzyInputLabelResolver>();
        services.AddSingleton<IStoreInputValueResolver, StoreInputValueResolver>();

        services.AddValidatedOptions<SyncSettings, SyncSettingsValidator>(SyncSettings.SectionName);
        services.AddValidatedOptions<RedemptionsOptions, RedemptionsOptionsValidator>(
            RedemptionsOptions.SectionName
        );
        services.AddValidatedOptions<InputResolutionOptions, InputResolutionOptionsValidator>(
            InputResolutionOptions.SectionName
        );
        services.AddValidatedOptions<CredentialExpiryOptions, CredentialExpiryOptionsValidator>(
            CredentialExpiryOptions.SectionName
        );

        return services;
    }

    private static void AddIdentityUseCases(this IServiceCollection services)
    {
        services.AddScoped<ProviderSecretProtectors>();
        services.AddScoped<ProviderSecrets>();
        services.AddScoped<SetProviderCredential>();
        services.AddScoped<ClearProviderCredential>();
        services.AddScoped<SetUserLocale>();
        services.AddScoped<RefreshProviderAccounts>();
        services.AddScoped<WarnExpiringCredentials>();
    }

    private static void AddStoreUseCases(this IServiceCollection services)
    {
        services.AddScoped<AddWatchedStore>();
        services.AddScoped<ListWatchedStores>();
        services.AddScoped<GetStoreDetail>();
        services.AddScoped<ListStoreItems>();
        services.AddScoped<GetStoreItemFacets>();
        services.AddScoped<GetWatchedStoreTotals>();
        services.AddScoped<ActivateStore>();
        services.AddScoped<DeactivateStore>();
        services.AddScoped<RemoveStore>();
        services.AddScoped<RefreshStoreChannelInfo>();

        services.AddScoped<AddWishlistEntry>();
        services.AddScoped<UpdateWishlistEntry>();
        services.AddScoped<RemoveWishlistEntry>();
        services.AddScoped<GetWishlistEntry>();
        services.AddScoped<ListWishlistEntriesGrouped>();

        services.AddScoped<GetStoreInputProfile>();
        services.AddScoped<UpsertStoreProfileValues>();
        services.AddScoped<UpsertStoreProfileAlias>();
        services.AddScoped<DeleteStoreProfileValue>();
        services.AddScoped<DeleteStoreProfileAlias>();

        services.AddScoped<SyncStores>();
        services.AddScoped<SubmitRedemptions>();
        services.AddScoped<PollRedemptionStatus>();
        services.AddScoped<ListRedemptions>();
        services.AddScoped<DeliverFulfillmentCodes>();

        // Per-store units of a sync cycle: the stores of a cycle are processed in parallel, so each
        // one takes a unit of its own rather than sharing the coordinator's scoped repositories.
        services.AddScopedUnit<StoreSyncUnit>();
        services.AddScopedUnit<StoreInputCoverageUnit>();
    }

    /// <summary>
    /// Registers <typeparamref name="T"/> as scoped, plus the
    /// <c>Func&lt;ScopedInstance&lt;T&gt;&gt;</c> that hands out one instance per fresh scope. The
    /// container is reached only here, in the registration itself: what consumes the factory
    /// declares it as a constructor parameter and never resolves anything by type at call time.
    /// </summary>
    private static void AddScopedUnit<T>(this IServiceCollection services)
        where T : class
    {
        services.AddScoped<T>();
        services.AddSingleton<Func<ScopedInstance<T>>>(serviceProvider =>
        {
            var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

            return () =>
            {
                var scope = scopeFactory.CreateAsyncScope();
                return new ScopedInstance<T>(scope.ServiceProvider.GetRequiredService<T>(), scope);
            };
        });
    }
}
