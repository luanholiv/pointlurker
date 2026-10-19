using System.Text.Json.Serialization;

namespace PointLurker.Core.Domain.ValueObjects;

public readonly record struct ItemCooldown
{
    [JsonConstructor]
    public ItemCooldown(long userMs, long globalMs, long categoryMs)
    {
        if (userMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userMs));
        }

        if (globalMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(globalMs));
        }

        if (categoryMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(categoryMs));
        }

        UserMs = userMs;
        GlobalMs = globalMs;
        CategoryMs = categoryMs;
    }

    public long UserMs { get; }
    public long GlobalMs { get; }
    public long CategoryMs { get; }
}
