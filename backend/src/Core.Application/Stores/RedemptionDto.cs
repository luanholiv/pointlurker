using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One redemption as the history shows it.
/// <para>
/// Field-for-field with <see cref="RedemptionListRow"/> on purpose rather than by accident: the row is
/// the shape the query returns and has to live in <c>Core.Domain</c> because a repository signature
/// names it, while this is the shape this layer hands outward and what the response contract converts
/// from. Keeping them distinct is what stops a persistence read shape from becoming the wire contract.
/// </para>
/// </summary>
public sealed record RedemptionDto(
    Guid Id,
    RedemptionStatus Status,
    string StoreItemName,
    string StoreName,
    DateTimeOffset AttemptedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? SettledAt,
    SubmissionFailureCode? FailureCode,
    string? FailureContext,
    int PollFailures,
    StoreProvider Provider
)
{
    public static explicit operator RedemptionDto(RedemptionListRow row) =>
        new(
            row.Id,
            row.Status,
            row.StoreItemName,
            row.StoreName,
            row.AttemptedAt,
            row.SubmittedAt,
            row.SettledAt,
            row.FailureCode,
            row.FailureContext,
            row.PollFailures,
            row.Provider
        );
}
