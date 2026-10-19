using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Notifications;

/// <summary>The email warning the owner that a provider credential is about to stop working.</summary>
/// <param name="HasExpired">
/// Whether <paramref name="ExpiresAt"/> had already passed when the warning was sent — a pass that
/// first sees a credential after its expiry still tells the owner once.
/// </param>
public sealed record CredentialExpiryEmailModel(
    StoreProvider Provider,
    DateTimeOffset ExpiresAt,
    bool HasExpired
);
