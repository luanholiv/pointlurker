using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal sealed record ChannelLookupResponse(
    [property: JsonPropertyName("_id")] string Id,
    string Username,
    string DisplayName
)
{
    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    public static explicit operator ProviderStoreLookup(ChannelLookupResponse dto) =>
        new(new StoreExternalId(dto.Id), dto.Username, dto.DisplayName, dto.Avatar);
}
