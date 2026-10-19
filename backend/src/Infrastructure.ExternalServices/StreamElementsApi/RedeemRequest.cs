using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal sealed record RedeemRequest
{
    [JsonPropertyName("input")]
    public required IReadOnlyList<string> Input { get; init; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>
    /// StreamElements takes inputs positionally, in the item's definition order, which is the order
    /// the command carries them in. The labels stay on this side of the wire.
    /// </summary>
    public static explicit operator RedeemRequest(ProviderRedeemCommand command) =>
        new()
        {
            Input = command.Input.Select(value => value.Value).ToArray(),
            Message = command.Message,
        };
}
