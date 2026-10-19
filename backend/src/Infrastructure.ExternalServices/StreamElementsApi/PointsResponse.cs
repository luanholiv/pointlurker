using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal sealed record PointsResponse
{
    [JsonPropertyName("points")]
    public long Points { get; init; }
}
