using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.Database.ValueConverters;

public static class ValueObjectConverters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly ValueConverter<StoreExternalId, string> StoreExternalIdConverter = new(
        value => value.Value,
        value => new StoreExternalId(value)
    );

    public static readonly ValueConverter<ProviderItemId, string> ProviderItemIdConverter = new(
        value => value.Value,
        value => new ProviderItemId(value)
    );

    public static readonly ValueConverter<ItemAvailability, bool> ItemAvailabilityConverter = new(
        value => value.IsAvailable,
        value => new ItemAvailability(value)
    );

    public static readonly ValueConverter<ItemCooldown, string> ItemCooldownConverter = new(
        value => JsonSerializer.Serialize(value, JsonOptions),
        json => JsonSerializer.Deserialize<ItemCooldown>(json, JsonOptions)
    );
}
