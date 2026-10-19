using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>The signed-in account. The site reads every member as optional, and so does this.</summary>
internal sealed record GauPointsUserResponse
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
