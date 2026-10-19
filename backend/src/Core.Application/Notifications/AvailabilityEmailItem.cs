namespace PointLurker.Core.Application.Notifications;

/// <summary>
/// One newly available item as the email presents it.
/// </summary>
/// <param name="RedemptionPending">
/// The item was auto-redeemed during this sync pass and the redemption has not been submitted yet.
/// </param>
/// <param name="QuantityCurrent">
/// Remaining stock, or <c>-1</c> for unlimited — in which case the email omits the stock line,
/// matching what the console's item card does.
/// </param>
public sealed record AvailabilityEmailItem(
    string ItemName,
    long Cost,
    bool RedemptionPending,
    string CategoryName,
    bool SubscriberOnly,
    int QuantityCurrent,
    int QuantityTotal
);
