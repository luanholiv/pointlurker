namespace PointLurker.Core.Application.Stores;

public sealed class RedemptionsOptions
{
    public const string SectionName = "Redemptions";

    public string PollingCronExpression { get; set; } = "* * * * *";

    public int CompletionTimeoutHours { get; set; } = 24;

    /// <summary>
    /// Consecutive failed completion checks on one redemption before the failure is escalated from
    /// a per-tick warning to an error.
    /// </summary>
    /// <remarks>
    /// Only the log level changes — a redemption is never failed for this, because an unreadable
    /// status says nothing about whether the submission worked. The default is deliberately short
    /// relative to <see cref="CompletionTimeoutHours"/>: on the default one-minute schedule it
    /// escalates about ten minutes in, so a broken contract is loud long before the rows age out.
    /// </remarks>
    public int PollFailureAlertThreshold { get; set; } = 10;

    /// <summary>
    /// How often the submission service sweeps for pending redemptions on its own, independently of
    /// a newly created one arriving.
    /// </summary>
    /// <remarks>
    /// The sweep is what makes a scheduled retry land. A transient submission failure sets a future
    /// retry time, but the service is otherwise woken only by a newly created redemption or by a
    /// process restart — so without this floor a lone pending row could wait indefinitely for an
    /// unrelated redemption to be created.
    /// </remarks>
    public int RetrySweepIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// How often the gift-card delivery sweeps for undelivered codes on its own, independently of a
    /// newly stored one arriving.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="RetrySweepIntervalMinutes"/> because the two wait on different
    /// things: a mail server that stalls must not set the pace of redemption retries, nor the
    /// reverse. The sweep is what lands a delivery retry, so it bounds how late one can be.
    /// </remarks>
    public int FulfillmentSweepIntervalMinutes { get; set; } = 5;
}
