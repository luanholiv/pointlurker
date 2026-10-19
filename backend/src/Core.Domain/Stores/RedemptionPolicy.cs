namespace PointLurker.Core.Domain.Stores;

/// <summary>
/// The caller-supplied half of the auto-redeem gate: how many redemptions of this entry already
/// hold quota, and the shortest interval the caller will allow between two of them.
/// <para>
/// Both are computed outside the entity — the count from the redemption history, the interval from
/// configuration — so the entity needs no repository and learns nothing about which provider the
/// item came from. The interval is a floor under the item's own cooldown, never a replacement: an
/// item declaring a longer cooldown keeps it.
/// </para>
/// </summary>
public readonly record struct RedemptionPolicy(int QuotaUsed, TimeSpan MinimumInterval);
