using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PointLurker.Infrastructure.Database.ValueConverters;

/// <summary>
/// Maps a read-only list to a <c>jsonb</c> column. Replaces the three converters that differed only
/// in their type argument (<c>string</c>, <c>StoreInputValue</c>, <c>StoreInputAlias</c>).
/// <para>
/// The serialization contract is load-bearing, not incidental: these columns already hold data.
/// <see cref="JsonSerializerDefaults.Web"/> is what makes the payload camelCase, and the empty-list
/// fallback is what keeps a JSON <c>null</c> from surfacing as a null collection on an aggregate that
/// treats the list as always present. Both are pinned by <c>JsonListConverterTests</c>.
/// </para>
/// </summary>
public sealed class JsonListConverter<T> : ValueConverter<IReadOnlyList<T>, string>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public JsonListConverter()
        : base(
            list => JsonSerializer.Serialize(list, JsonOptions),
            json => JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>()
        ) { }
}
