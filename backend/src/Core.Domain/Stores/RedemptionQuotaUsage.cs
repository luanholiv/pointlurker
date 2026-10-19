namespace PointLurker.Core.Domain.Stores;

/// <summary>
/// What one wishlist entry's redemption history already holds: <paramref name="QuotaUsed"/> counts
/// every redemption that holds a slot against the entry's desired count, and
/// <paramref name="Inflight"/> the subset still awaiting an upstream outcome.
/// <para>
/// Two numbers rather than one because they bound different things. The quota bounds the total an
/// entry may ever hold; the inflight count bounds concurrency, so one entry never has two
/// submissions racing each other. A quota of three with one submission in flight leaves two slots
/// free and still must wait.
/// </para>
/// </summary>
public readonly record struct RedemptionQuotaUsage(int QuotaUsed, int Inflight);
