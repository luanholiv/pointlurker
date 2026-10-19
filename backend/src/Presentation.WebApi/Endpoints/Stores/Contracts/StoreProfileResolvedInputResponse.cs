using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreProfileResolvedInputResponse(
    string LabelCanonical,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<StoreProfileRequesterResponse> RequestedBy
)
{
    public static explicit operator StoreProfileResolvedInputResponse(ResolvedInputDto resolved) =>
        new(
            resolved.LabelCanonical,
            resolved.UpdatedAt,
            [.. resolved.RequestedBy.Select(r => (StoreProfileRequesterResponse)r)]
        );
}
