using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using Polly;
using Refit;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

public static class GauPointsServiceCollectionExtensions
{
    /// <summary>
    /// Identifies this application to a small self-hosted site that publishes no rate limits, so
    /// its operators can tell the traffic apart and reach out rather than block it blind.
    /// </summary>
    private const string UserAgent = "PointLurker/1.0 (self-hosted store monitor)";

    public static IServiceCollection AddGauPointsApi(this IServiceCollection services)
    {
        services.AddValidatedOptions<GauPointsSettings>(GauPointsSettings.SectionName);

        services
            .AddRefitGeneratedClient<IGauPointsApi>()
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<GauPointsSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                }
            )
            .AddResilienceHandler(
                "gaupoints-api",
                builder =>
                {
                    var retry = new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 2,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(2),
                        UseJitter = true,
                    };

                    // A redeem is never repeated here, idempotency key or not: a provider
                    // de-duplicating must never be the only thing between a 5xx and a double
                    // spend. SubmitRedemptions owns that retry, counted and durable.
                    retry.DisableForUnsafeHttpMethods();
                    builder.AddRetry(retry);
                }
            );

        services.AddKeyedScoped<IStoreProviderClient>(
            StoreProvider.GauPoints,
            (sp, _) =>
                new GauPointsApiClient(
                    sp.GetRequiredService<IGauPointsApi>(),
                    sp.GetRequiredService<IOptions<GauPointsSettings>>()
                )
        );

        // TryAdd: every provider's registration needs the factory, and it must exist once.
        services.TryAddScoped<IStoreProviderClientFactory, StoreProviderClientFactory>();

        return services;
    }
}
