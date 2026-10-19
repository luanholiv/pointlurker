using System.Text.Json.Serialization;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>
/// The redeem answer. The failure envelope is observed; <c>message</c> and <c>code</c> on success
/// are derived from the site's client, because a success has never been captured.
/// </summary>
internal sealed record GauPointsRedeemResponse
{
    /// <summary>Null when the key is missing, which is not the same as <c>false</c>.</summary>
    [JsonPropertyName("ok")]
    public bool? Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// The gift-card code, returned once and never again. A bearer secret: never logged, and never
    /// written to a failure context.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }
}
