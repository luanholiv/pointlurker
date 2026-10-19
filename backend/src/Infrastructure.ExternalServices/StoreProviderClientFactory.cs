using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Infrastructure.ExternalServices;

/// <summary>
/// Resolves provider clients registered keyed by <see cref="StoreProvider"/>. Lives here, beside the
/// registrations, so the application layer can pick a client without touching the container.
/// </summary>
internal sealed class StoreProviderClientFactory(IServiceProvider services)
    : IStoreProviderClientFactory
{
    public IStoreProviderClient For(StoreProvider provider) =>
        services.GetKeyedService<IStoreProviderClient>(provider)
        ?? throw new InvalidOperationException(
            $"No {nameof(IStoreProviderClient)} is registered for {provider}."
        );
}
