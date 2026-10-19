using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>
/// One catalogue entry. <c>stock</c> and <c>image</c> are optional: every live item carries both,
/// but the site's own client guards them, so this contract does too.
/// </summary>
internal sealed record GauPointsItemResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("cost")]
    public long Cost { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("stock")]
    public int? Stock { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    /// <summary>
    /// Stock maps onto the domain's quantity with no unlimited sentinel: <c>(stock, stock)</c>,
    /// always enabled, so the existing availability rule yields <c>stock &gt; 0</c>. A missing stock
    /// reads as none left — the safe direction for a store that spends real points.
    /// </summary>
    public static explicit operator ProviderStoreItem(GauPointsItemResponse dto)
    {
        var stock = Math.Max(dto.Stock ?? 0, 0);

        return new ProviderStoreItem(
            StoreProvider.GauPoints,
            new ProviderItemId(dto.Id),
            GauPointsStore.ExternalId,
            dto.Name,
            dto.Description,
            dto.Type,
            dto.Cost,
            dto.Type,
            Enabled: true,
            Featured: false,
            Public: true,
            SubscriberOnly: false,
            AllowMessages: false,
            GauPointsInputs.DefinitionsFor(dto.Type),
            Sources: [],
            new ItemQuantity(stock, stock),
            new ItemCooldown(0, 0, 0),
            CreatedAt: null,
            UpdatedAt: null,
            ThumbnailUrl: dto.Image,
            PreviewUrl: dto.Image
        );
    }
}
