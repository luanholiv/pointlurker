namespace PointLurker.Core.Application.Stores;

/// <summary>A submitted redemption's state, read back from the provider.</summary>
/// <param name="Rejected">The provider accepted the submission and then refused to fulfil it.</param>
/// <param name="UpdatedAt">When the provider last changed the redemption; dates the outcome.</param>
public sealed record ProviderRedemptionStatus(
    bool Completed,
    bool Rejected,
    DateTimeOffset UpdatedAt
);
