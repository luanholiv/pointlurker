using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal sealed record CurrentUserResponse
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }
}
