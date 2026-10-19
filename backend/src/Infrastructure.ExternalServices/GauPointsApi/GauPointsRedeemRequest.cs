using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

internal sealed record GauPointsRedeemRequest
{
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    [JsonPropertyName("data")]
    public required GauPointsRedeemData Data { get; init; }

    /// <summary>
    /// GauPoints takes inputs keyed, not positionally. Each value arrives under the label this
    /// client declared for it, so the label is turned back into its wire key here.
    /// </summary>
    public static explicit operator GauPointsRedeemRequest(ProviderRedeemCommand command)
    {
        var byLabel = command.Input.ToDictionary(
            input => input.Label,
            input => input.Value,
            StringComparer.Ordinal
        );

        return new GauPointsRedeemRequest
        {
            ItemId = command.Item.Value,
            Data = new GauPointsRedeemData
            {
                FullName = byLabel.GetValueOrDefault(GauPointsInputs.FullName, string.Empty),
                Phone = byLabel.GetValueOrDefault(GauPointsInputs.Phone, string.Empty),
                Email = byLabel.GetValueOrDefault(GauPointsInputs.Email, string.Empty),
                Address = byLabel.GetValueOrDefault(GauPointsInputs.Address, string.Empty),
            },
        };
    }
}
