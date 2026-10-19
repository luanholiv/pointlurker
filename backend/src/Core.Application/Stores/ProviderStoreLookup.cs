using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Application.Stores;

/// <summary>A store found by <see cref="IStoreProviderClient.LookupStoreAsync"/>.</summary>
/// <param name="Handle">What a user types to find the store — for StreamElements, the channel username.</param>
/// <param name="ItemCount">
/// How many items the store lists, when the lookup read its catalogue on the way. Null when it did
/// not, which is not the same as an empty store.
/// </param>
public sealed record ProviderStoreLookup(
    StoreExternalId ExternalId,
    string Handle,
    string DisplayName,
    string? AvatarUrl = null,
    int? ItemCount = null
);
