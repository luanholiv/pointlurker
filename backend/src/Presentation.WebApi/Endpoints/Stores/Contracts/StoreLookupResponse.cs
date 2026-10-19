using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <param name="ItemCount">Null when the lookup did not read the catalogue, which is not an empty store.</param>
internal sealed record StoreLookupResponse(
    string ExternalId,
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    int? ItemCount
)
{
    public static explicit operator StoreLookupResponse(ProviderStoreLookup lookup) =>
        new(
            lookup.ExternalId.Value,
            lookup.Handle,
            lookup.DisplayName,
            lookup.AvatarUrl,
            lookup.ItemCount
        );
}
