namespace PointLurker.Core.Domain.Stores;

public enum RedemptionStatus
{
    Pending,
    Submitted,
    Completed,
    SubmissionFailed,
    CompletionTimedOut,

    /// <summary>
    /// The provider accepted the submission and then refused to fulfil it (StreamElements reports
    /// <c>rejected</c> on its redemption lookup). Terminal, and distinct from a timeout: the outcome is known, not missing.
    /// </summary>
    Rejected,
}
