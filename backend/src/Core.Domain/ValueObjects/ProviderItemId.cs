namespace PointLurker.Core.Domain.ValueObjects;

public readonly record struct ProviderItemId
{
    public ProviderItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ProviderItemId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
