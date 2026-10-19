using System.Text.Json.Serialization;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal sealed record StoreItemResponse
{
    [JsonPropertyName("_id")]
    public required string Id { get; init; }

    [JsonPropertyName("channel")]
    public required string Channel { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("cost")]
    public long Cost { get; init; }

    [JsonPropertyName("categoryName")]
    public string CategoryName { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("featured")]
    public bool Featured { get; init; }

    [JsonPropertyName("public")]
    public bool Public { get; init; }

    [JsonPropertyName("subscriberOnly")]
    public bool SubscriberOnly { get; init; }

    [JsonPropertyName("allowMessages")]
    public bool AllowMessages { get; init; }

    [JsonPropertyName("userInput")]
    public IReadOnlyList<string> UserInput { get; init; } = [];

    [JsonPropertyName("sources")]
    public IReadOnlyList<string> Sources { get; init; } = [];

    [JsonPropertyName("quantity")]
    public required QuantityResponse Quantity { get; init; }

    [JsonPropertyName("cooldown")]
    public required CooldownResponse Cooldown { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("preview")]
    public string? Preview { get; init; }

    public static explicit operator ProviderStoreItem(StoreItemResponse dto) =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId(dto.Id),
            new StoreExternalId(dto.Channel),
            dto.Name,
            dto.Description,
            dto.Type,
            dto.Cost,
            dto.CategoryName,
            dto.Enabled,
            dto.Featured,
            dto.Public,
            dto.SubscriberOnly,
            dto.AllowMessages,
            dto.UserInput,
            dto.Sources,
            new ItemQuantity(dto.Quantity.Total, dto.Quantity.Current),
            new ItemCooldown(dto.Cooldown.User, dto.Cooldown.Global, dto.Cooldown.Category),
            dto.CreatedAt,
            dto.UpdatedAt,
            dto.Thumbnail,
            dto.Preview
        );
}

internal sealed record QuantityResponse
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("current")]
    public int Current { get; init; }
}

internal sealed record CooldownResponse
{
    [JsonPropertyName("user")]
    public long User { get; init; }

    [JsonPropertyName("global")]
    public long Global { get; init; }

    [JsonPropertyName("category")]
    public long Category { get; init; }
}
