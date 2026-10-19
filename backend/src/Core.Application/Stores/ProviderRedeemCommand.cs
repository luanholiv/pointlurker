using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Application.Stores;

/// <param name="RedemptionId">
/// The durable id of the redemption being submitted. A provider that de-duplicates requests uses it
/// as the idempotency key, so a retry hours later is still recognised as the same redemption.
/// </param>
/// <param name="Input">In the item's definition order.</param>
public sealed record ProviderRedeemCommand(
    Guid RedemptionId,
    StoreExternalId Store,
    ProviderItemId Item,
    IReadOnlyList<ProviderInputValue> Input,
    string? Message
);
