namespace PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;

/// <summary>
/// The query string of <c>GET /api/redemptions</c>. Raw strings so the endpoint, not the model
/// binder, decides what an uninterpretable filter answers with.
/// </summary>
internal sealed record RedemptionListQuery(
    string? Status,
    string? WishlistEntryId,
    string? From,
    string? To,
    string? Sort,
    string? Dir,
    string? Offset,
    string? Limit
);
