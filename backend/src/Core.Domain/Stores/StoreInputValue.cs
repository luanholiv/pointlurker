namespace PointLurker.Core.Domain.Stores;

public sealed record StoreInputValue(
    string LabelCanonical,
    string EncryptedValue,
    DateTimeOffset UpdatedAt
);
