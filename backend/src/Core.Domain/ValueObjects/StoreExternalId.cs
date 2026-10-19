namespace PointLurker.Core.Domain.ValueObjects;

public readonly record struct StoreExternalId
{
    public StoreExternalId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("StoreExternalId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
