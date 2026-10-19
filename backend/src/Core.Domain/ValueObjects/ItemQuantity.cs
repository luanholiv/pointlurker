using System.Text.Json.Serialization;

namespace PointLurker.Core.Domain.ValueObjects;

public readonly record struct ItemQuantity
{
    [JsonConstructor]
    public ItemQuantity(int total, int current)
    {
        if (total < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(total));
        }

        if (current < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(current));
        }

        Total = total;
        Current = current;
    }

    public int Total { get; }
    public int Current { get; }
}
