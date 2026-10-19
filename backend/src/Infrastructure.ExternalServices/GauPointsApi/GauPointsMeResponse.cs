using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>The session state. Signed out, only <c>logged:false</c> is present.</summary>
internal sealed record GauPointsMeResponse
{
    [JsonPropertyName("logged")]
    public bool Logged { get; init; }

    [JsonPropertyName("points")]
    public long? Points { get; init; }

    [JsonPropertyName("user")]
    public GauPointsUserResponse? User { get; init; }
}
