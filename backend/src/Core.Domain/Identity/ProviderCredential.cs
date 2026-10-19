using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Domain.Identity;

/// <summary>
/// The owner's secret for one store provider, with the account it authenticates as. At most one per
/// <c>(UserId, Provider)</c>.
/// </summary>
public sealed class ProviderCredential
{
    /// <summary>
    /// Upper bound on <see cref="LastValidationError"/>. It holds an error code, which is far
    /// shorter; the bound keeps provider text from ever fitting there by accident.
    /// </summary>
    public const int LastValidationErrorMaxLength = 128;

    private ProviderCredential() { }

    public ProviderCredential(
        Guid userId,
        StoreProvider provider,
        string encryptedSecret,
        DateTimeOffset setAt
    )
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A credential must belong to a user.", nameof(userId));

        UserId = userId;
        Provider = provider;
        ReplaceSecret(encryptedSecret, setAt);
    }

    public Guid UserId { get; private set; }
    public StoreProvider Provider { get; private set; }

    /// <summary>Data Protection ciphertext. The plain secret never reaches this entity.</summary>
    public string EncryptedSecret { get; private set; } = string.Empty;

    public DateTimeOffset SecretSetAt { get; private set; }

    /// <summary>
    /// When the secret stops working, when the secret itself says so — a GauPoints session is a JWT
    /// carrying <c>exp</c>. A lower bound on validity, never a guarantee: the provider can end a
    /// session early. Null for a secret that does not announce an expiry.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// The <see cref="ExpiresAt"/> the owner was last warned about. Compared against the current
    /// expiry rather than cleared with the secret, so a new session re-arms the warning while the
    /// same session pasted again does not repeat it.
    /// </summary>
    public DateTimeOffset? ExpiryWarningSentFor { get; private set; }

    /// <summary>
    /// The account name the provider reports for this secret. Null until the first successful
    /// lookup, and never cleared by a failed one.
    /// </summary>
    public string? AccountName { get; private set; }

    public DateTimeOffset? AccountNameRefreshedAt { get; private set; }

    /// <summary>When the provider last accepted this secret. Null for a secret never confirmed.</summary>
    public DateTimeOffset? LastValidatedAt { get; private set; }

    /// <summary>
    /// The error code of the last failed validation, cleared by the next success. An error code
    /// only: provider response text can echo the secret, so it never lands here.
    /// </summary>
    public string? LastValidationError { get; private set; }

    /// <summary>
    /// Swaps in a new secret. Whatever was known about the old secret's validity does not carry
    /// over, its expiry included; the account name does, until a lookup replaces it.
    /// </summary>
    public void ReplaceSecret(string encryptedSecret, DateTimeOffset setAt)
    {
        if (string.IsNullOrWhiteSpace(encryptedSecret))
            throw new ArgumentException("Secret cannot be empty.", nameof(encryptedSecret));

        EncryptedSecret = encryptedSecret;
        SecretSetAt = setAt;
        ExpiresAt = null;
        LastValidatedAt = null;
        LastValidationError = null;
    }

    /// <summary>Records the expiry the current secret announces, or null when it announces none.</summary>
    public void SetExpiresAt(DateTimeOffset? expiresAt) => ExpiresAt = expiresAt;

    /// <summary>
    /// Whether the secret's own expiry has passed at <paramref name="now"/>. False for a secret
    /// with no known expiry: absence of a deadline is not a failure.
    /// </summary>
    public bool IsExpiredAt(DateTimeOffset now) => ExpiresAt is { } expiresAt && expiresAt <= now;

    /// <summary>
    /// Whether the owner is still owed a warning about the current expiry at
    /// <paramref name="now"/>: the expiry falls within <paramref name="window"/>, or has already
    /// passed, and no warning went out for it.
    /// </summary>
    public bool NeedsExpiryWarning(DateTimeOffset now, TimeSpan window) =>
        ExpiresAt is { } expiresAt
        && expiresAt - now <= window
        && ExpiryWarningSentFor != expiresAt;

    /// <summary>Records that the owner was warned about the current expiry.</summary>
    public void MarkExpiryWarningSent() =>
        ExpiryWarningSentFor =
            ExpiresAt
            ?? throw new InvalidOperationException(
                "A credential with no expiry cannot have been warned about one."
            );

    /// <summary>
    /// Persists the account name the provider resolved. Callers invoke this only on a successful
    /// lookup — never to clear a known value.
    /// </summary>
    public void SetAccountName(string accountName, DateTimeOffset refreshedAt)
    {
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("Account name cannot be empty.", nameof(accountName));

        AccountName = accountName;
        AccountNameRefreshedAt = refreshedAt;
    }

    public void MarkValidated(DateTimeOffset validatedAt)
    {
        LastValidatedAt = validatedAt;
        LastValidationError = null;
    }

    public void MarkValidationFailed(string errorCode)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("An error code is required.", nameof(errorCode));

        LastValidationError = errorCode;
    }
}
