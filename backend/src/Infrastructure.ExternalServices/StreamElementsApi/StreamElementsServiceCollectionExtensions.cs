using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using Polly;
using Refit;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

public static class StreamElementsServiceCollectionExtensions
{
    public static IServiceCollection AddStreamElementsApi(this IServiceCollection services)
    {
        services.AddValidatedOptions<StreamElementsSettings>(StreamElementsSettings.SectionName);

        services
            .AddRefitGeneratedClient<IStreamElementsApi>()
            .ConfigureHttpClient(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<StreamElementsSettings>>().Value;
                    client.BaseAddress = new Uri(settings.BaseUrl);
                }
            )
            .AddResilienceHandler(
                "se-api",
                builder =>
                {
                    var retry = new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(2),
                        UseJitter = false,
                    };

                    // A redeem that answers 5xx may already exist upstream, and StreamElements
                    // honours no idempotency key: retrying it here could spend the points twice.
                    // SubmitRedemptions owns that retry, counted and durable.
                    retry.DisableForUnsafeHttpMethods();
                    builder.AddRetry(retry);
                }
            );

        services.AddKeyedScoped<IStoreProviderClient>(
            StoreProvider.StreamElements,
            (sp, _) => new StreamElementsApiClient(sp.GetRequiredService<IStreamElementsApi>())
        );

        // TryAdd: every provider's registration needs the factory, and it must exist once.
        services.TryAddScoped<IStoreProviderClientFactory, StoreProviderClientFactory>();

        return services;
    }
}
