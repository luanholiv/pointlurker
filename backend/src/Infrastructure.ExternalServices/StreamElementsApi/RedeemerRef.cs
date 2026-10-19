using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

/// <summary>
/// The expanded <c>redeemer</c> object on a single-redemption lookup.
/// </summary>
/// <remarks>
/// Nothing downstream reads this yet. It is declared anyway so the contract records that the
/// lookup expands the field into an object where the redeem call returns a bare id string —
/// the same asymmetry as <c>item</c>, and the trap a future edit would otherwise fall into.
/// </remarks>
internal sealed record RedeemerRef
{
    [JsonPropertyName("_id")]
    public required string Id { get; init; }

    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("avatar")]
    public string? Avatar { get; init; }

    [JsonPropertyName("inactive")]
    public bool Inactive { get; init; }

    [JsonPropertyName("isPartner")]
    public bool IsPartner { get; init; }
}
