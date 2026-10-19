namespace PointLurker.Core.Domain.Stores;

/// <summary>
/// Which redemption states occupy a slot against <see cref="WishlistEntry.DesiredRedemptions"/>.
/// <para>
/// A slot is held by a redemption that succeeded or may still succeed. The three failure states
/// release theirs: a quota consumed by a submission that failed would strand the entry forever,
/// spending the user's stated intent on something they never received.
/// </para>
/// </summary>
public static class RedemptionQuota
{
    /// <summary>
    /// Declared as an array so the database side can translate it to an <c>IN</c> list; the states
    /// are stored by name, so the query reads the same as this does.
    /// </summary>
    public static readonly RedemptionStatus[] HoldingStatuses =
    [
        RedemptionStatus.Pending,
        RedemptionStatus.Submitted,
        RedemptionStatus.Completed,
    ];

    /// <summary>The two states whose outcome is still unknown upstream.</summary>
    public static readonly RedemptionStatus[] InflightStatuses =
    [
        RedemptionStatus.Pending,
        RedemptionStatus.Submitted,
    ];

    public static bool Holds(RedemptionStatus status) => HoldingStatuses.Contains(status);

    public static bool IsInflight(RedemptionStatus status) => InflightStatuses.Contains(status);
}
