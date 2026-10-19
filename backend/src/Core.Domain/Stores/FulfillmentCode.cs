namespace PointLurker.Core.Domain.Stores;

/// <summary>
/// A gift-card code a provider returned for one redemption, and the state of delivering it to the
/// owner. The code exists in exactly one upstream response and can never be requested again, so the
/// row is kept after delivery and is never pruned.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="Redemption"/>: a redemption is an append-only snapshot read by every
/// history query, and a mutable bearer secret with its own delivery state belongs on neither.
/// </remarks>
public sealed class FulfillmentCode
{
    /// <summary>Matches the persisted column width; see the entity configuration.</summary>
    public const int MaxLastErrorLength = 512;

    private FulfillmentCode() { }

    /// <param name="encryptedCode">The code as ciphertext. The plaintext never reaches the domain.</param>
    public FulfillmentCode(
        Guid id,
        Guid redemptionId,
        string encryptedCode,
        DateTimeOffset createdAt
    )
    {
        if (string.IsNullOrWhiteSpace(encryptedCode))
            throw new ArgumentException("Encrypted code cannot be empty.", nameof(encryptedCode));

        Id = id;
        RedemptionId = redemptionId;
        EncryptedCode = encryptedCode;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid RedemptionId { get; private set; }
    public string EncryptedCode { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the owner was emailed the code. Null while delivery is still owed.</summary>
    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Delivery attempts, the successful one included.</summary>
    public int Attempts { get; private set; }

    /// <summary>The earliest time the next delivery attempt may run. Null means now.</summary>
    public DateTimeOffset? NextRetryAt { get; private set; }

    /// <summary>
    /// Why the most recent attempt failed. Diagnostic only, and never holds the code itself.
    /// </summary>
    public string? LastError { get; private set; }

    public void MarkSent(DateTimeOffset sentAt)
    {
        EnsureUndelivered();

        SentAt = sentAt;
        Attempts++;
        NextRetryAt = null;
        LastError = null;
    }

    /// <remarks>
    /// There is no final failure: a code that cannot be delivered is still the owner's, so delivery
    /// keeps being retried rather than abandoned.
    /// </remarks>
    public void RecordFailedAttempt(string error, DateTimeOffset nextRetryAt)
    {
        EnsureUndelivered();

        Attempts++;
        NextRetryAt = nextRetryAt;
        LastError = error.Length > MaxLastErrorLength ? error[..MaxLastErrorLength] : error;
    }

    private void EnsureUndelivered()
    {
        if (SentAt is not null)
            throw new InvalidOperationException("The code has already been delivered.");
    }
}
