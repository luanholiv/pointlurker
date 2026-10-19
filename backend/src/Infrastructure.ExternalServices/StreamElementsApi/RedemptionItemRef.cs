using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

/// <summary>
/// The expanded <c>item</c> object on a single-redemption lookup. The redeem call sends the same
/// field as a bare id string, which is why the two responses cannot share one contract.
/// </summary>
internal sealed record RedemptionItemRef
{
    [JsonPropertyName("_id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("userInput")]
    public IReadOnlyList<string> UserInput { get; init; } = [];
}
