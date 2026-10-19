namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// The columns the redemption history presents, and therefore the fields it can be ordered by.
/// Member names match the response field names the client already uses as its sort keys.
/// </summary>
public enum RedemptionSortField
{
    Status,
    StoreItemName,
    StoreName,
    AttemptedAt,
    SubmittedAt,
    SettledAt,
}
