namespace PointLurker.Core.Domain.ValueObjects;

public readonly record struct ItemAvailability(bool IsAvailable)
{
    public static ItemAvailability From(bool enabled, ItemQuantity quantity)
    {
        var available = enabled && (quantity.Current == -1 || quantity.Current > 0);
        return new ItemAvailability(available);
    }
}
