using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

internal sealed record GauPointsItemsResponse
{
    [JsonPropertyName("items")]
    public IReadOnlyList<GauPointsItemResponse> Items { get; init; } = [];
}
