using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// Picks the <see cref="IStoreProviderClient"/> for a provider. Taken as a constructor dependency, so
/// this layer never reaches into the container itself.
/// </summary>
public interface IStoreProviderClientFactory
{
    /// <exception cref="InvalidOperationException">No client is registered for <paramref name="provider"/>.</exception>
    IStoreProviderClient For(StoreProvider provider);
}
