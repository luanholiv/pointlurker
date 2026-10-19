using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal sealed record RedemptionResponse
{
    [JsonPropertyName("_id")]
    public required string Id { get; init; }

    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("item")]
    public required string Item { get; init; }

    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    [JsonPropertyName("input")]
    public IReadOnlyList<string> Input { get; init; } = [];

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    public static explicit operator ProviderRedemptionResult(RedemptionResponse dto) =>
        new ProviderRedemptionResult.Accepted(dto.Id, dto.Completed, FulfillmentCode: null);
}
