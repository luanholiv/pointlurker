using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

/// <summary>
/// <c>GET /kappa/v2/store/{channel}/redemptions/{redemption}</c>.
/// </summary>
/// <remarks>
/// Not <see cref="RedemptionResponse"/>, which models the redeem <c>POST</c>. The lookup returns
/// <c>item</c> and <c>redeemer</c> as objects, carries <c>rejected</c>, and drops <c>message</c>;
/// the redeem call returns both as bare id strings and carries <c>message</c>. One shared contract
/// therefore deserializes for one endpoint and throws for the other. Keep them apart.
/// </remarks>
internal sealed record GetRedemptionResponse
{
    [JsonPropertyName("_id")]
    public required string Id { get; init; }

    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("item")]
    public required RedemptionItemRef Item { get; init; }

    [JsonPropertyName("redeemer")]
    public RedeemerRef? Redeemer { get; init; }

    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    [JsonPropertyName("rejected")]
    public bool Rejected { get; init; }

    [JsonPropertyName("input")]
    public IReadOnlyList<string> Input { get; init; } = [];

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    public static explicit operator ProviderRedemptionStatus(GetRedemptionResponse dto) =>
        new(dto.Completed, dto.Rejected, dto.UpdatedAt);
}
