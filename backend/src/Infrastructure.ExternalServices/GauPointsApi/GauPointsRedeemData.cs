using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>
/// The recipient details. All four keys always travel, the absent ones as empty strings: that is
/// the only shape the site's own form has ever sent.
/// </summary>
internal sealed record GauPointsRedeemData
{
    [JsonPropertyName("fullName")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("phone")]
    public string Phone { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;
}
