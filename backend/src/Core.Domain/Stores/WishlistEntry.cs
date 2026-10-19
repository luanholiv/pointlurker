namespace PointLurker.Core.Domain.Stores;

public sealed class WishlistEntry
{
    /// <summary>
    /// Maximum length of a redemption message (plaintext). Enforced by the request validator, which
    /// reports it per-field as <c>validation.too_long</c> before the value is encrypted — once
    /// protected, the stored value is ciphertext that exceeds this bound.
    /// </summary>
    public const int RedemptionMessageMaxLength = 1000;

    /// <summary>
    /// Upper bound on <see cref="DesiredRedemptions"/>, enforced by the request validator and
    /// guarded here as a backstop. A safety rail rather than a formality: this field authorises
    /// irreversible spending, so a fat-fingered quota must not be expressible. Unlimited is
    /// <see langword="null"/>, which a caller has to choose deliberately.
    /// </summary>
    public const int DesiredRedemptionsMax = 10;

    private WishlistEntry() { }

    public WishlistEntry(Guid id, Guid userId, Guid storeItemId, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        StoreItemId = storeItemId;
        NotifyOnAvailable = true;
        AutoRedeem = false;
        DesiredRedemptions = 1;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid StoreItemId { get; private set; }
    public bool NotifyOnAvailable { get; private set; }
    public bool AutoRedeem { get; private set; }
    public bool IsBlockedOnInput { get; private set; }

    /// <summary>
    /// How many redemptions auto-redeem may hold for this entry in total. <see langword="null"/> is
    /// unlimited. Counted over redemptions that hold quota, not over attempts — a failed submission
    /// releases its slot, or a single failure would strand the entry forever.
    /// </summary>
    public int? DesiredRedemptions { get; private set; }

    public string? RedemptionMessage { get; private set; }
    public DateTimeOffset? LastRedemptionCreatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public StoreItem? StoreItem { get; private set; }

    public void SetNotifyOnAvailable(bool notify, DateTimeOffset now)
    {
        NotifyOnAvailable = notify;
        UpdatedAt = now;
    }

    public void SetRedemptionMessage(string? message, bool allowMessages, DateTimeOffset now)
    {
        if (message is not null && !allowMessages)
        {
            throw new InvalidOperationException(
                "Cannot set a redemption message for an item that does not allow messages."
            );
        }

        RedemptionMessage = message;
        UpdatedAt = now;
    }

    public void SetDesiredRedemptions(int? desired, DateTimeOffset now)
    {
        if (desired is int value and (< 1 or > DesiredRedemptionsMax))
        {
            throw new ArgumentOutOfRangeException(
                nameof(desired),
                value,
                $"Desired redemptions must be between 1 and {DesiredRedemptionsMax}, or null for unlimited."
            );
        }

        DesiredRedemptions = desired;
        UpdatedAt = now;
    }

    /// <summary>
    /// Whether the entry may hold one more redemption, given how many already hold quota. Exposed
    /// apart from <see cref="CanCreateRedemption"/> so a caller can tell a quota refusal from a
    /// cooldown refusal without re-deriving the rule.
    /// </summary>
    public bool HasQuotaRemaining(int quotaUsed) =>
        DesiredRedemptions is not int desired || quotaUsed < desired;

    public void EnableAutoRedeem(DateTimeOffset now)
    {
        AutoRedeem = true;
        IsBlockedOnInput = false;
        UpdatedAt = now;
    }

    public void DisableAutoRedeem(DateTimeOffset now)
    {
        AutoRedeem = false;
        UpdatedAt = now;
    }

    public void BlockOnMissingInput(DateTimeOffset now)
    {
        AutoRedeem = false;
        IsBlockedOnInput = true;
        UpdatedAt = now;
    }

    public void ClearInputBlock(DateTimeOffset now)
    {
        IsBlockedOnInput = false;
        UpdatedAt = now;
    }

    public bool CanCreateRedemption(
        StoreItem item,
        DateTimeOffset now,
        long availableBalance,
        RedemptionPolicy policy
    )
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!AutoRedeem)
        {
            return false;
        }

        if (!HasQuotaRemaining(policy.QuotaUsed))
        {
            return false;
        }

        if (!item.Availability.IsAvailable)
        {
            return false;
        }

        if (item.Cost > availableBalance)
        {
            return false;
        }

        if (LastRedemptionCreatedAt is null)
        {
            return true;
        }

        // The policy's interval floors the item's own cooldown. An item declaring none — every
        // item of a provider without the concept — would otherwise reopen this gate on the next
        // tick, against a stock figure the sync has not refreshed yet.
        var cooldownMs = Math.Max(
            Math.Max(item.Cooldown.UserMs, item.Cooldown.GlobalMs),
            (long)policy.MinimumInterval.TotalMilliseconds
        );
        var unblocksAt = LastRedemptionCreatedAt.Value.AddMilliseconds(cooldownMs);
        return unblocksAt <= now;
    }

    public Redemption CreateRedemption(
        StoreItem storeItem,
        string? sentMessage,
        string storeItemName,
        string storeName,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(storeItem);

        LastRedemptionCreatedAt = now;
        UpdatedAt = now;

        return new Redemption(
            Guid.NewGuid(),
            Id,
            storeItem.Id,
            storeItemName,
            storeName,
            storeItem.Provider,
            storeItem.StoreExternalId,
            storeItem.ProviderItemId,
            sentMessage,
            now
        );
    }
}
