using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;

internal sealed record RedemptionItemResponse(
    Guid Id,
    string Status,
    string StoreItemName,
    string StoreName,
    DateTimeOffset AttemptedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? SettledAt,
    string? FailureCode,
    string? FailureContext,
    /// <summary>
    /// Consecutive failed completion checks on a submitted redemption. Non-zero says the outcome
    /// cannot currently be read, which is what tells a waiting row apart from a wedged one. The
    /// reason is not carried: it is a raw upstream diagnostic, and it belongs in the logs.
    /// </summary>
    int PollFailures,
    /// <summary>The provider slug, snapshotted on the redemption, so it outlives the store.</summary>
    string Provider
)
{
    public static explicit operator RedemptionItemResponse(RedemptionDto dto) =>
        new(
            dto.Id,
            // Neither enum crosses the wire as an ordinal and no JsonStringEnumConverter is
            // registered app-wide, so both conversions are explicit. The status goes as its member
            // name, which is the catalog key; the failure code goes as its `redemption.failure.*`
            // code, so a reader never has to keep its own copy of the enum to read the integer.
            dto.Status.ToString(),
            dto.StoreItemName,
            dto.StoreName,
            dto.AttemptedAt,
            dto.SubmittedAt,
            dto.SettledAt,
            dto.FailureCode is { } failureCode ? RedemptionFailureCodes.For(failureCode) : null,
            dto.FailureContext,
            dto.PollFailures,
            dto.Provider.ToSlug()
        );
}
