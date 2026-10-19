using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreProfileResponse(
    IReadOnlyList<StoreProfileUnresolvedInputResponse> Unresolved,
    IReadOnlyList<StoreProfileResolvedInputResponse> Resolved,
    IReadOnlyList<string> CanonicalLabels
)
{
    public static explicit operator StoreProfileResponse(StoreInputProfileResult profile) =>
        new(
            [.. profile.Unresolved.Select(u => (StoreProfileUnresolvedInputResponse)u)],
            [.. profile.Resolved.Select(r => (StoreProfileResolvedInputResponse)r)],
            profile.CanonicalLabels
        );
}
