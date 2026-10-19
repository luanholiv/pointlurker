using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreProfileUnresolvedInputResponse(
    string RawLabel,
    string? BestCanonical,
    int BestScore,
    string? AliasTarget,
    int ItemCount,
    int BlockedEntryCount
)
{
    public static explicit operator StoreProfileUnresolvedInputResponse(
        UnresolvedInputDto unresolved
    ) =>
        new(
            unresolved.RawLabel,
            unresolved.BestCanonical,
            unresolved.BestScore,
            unresolved.AliasTarget,
            unresolved.ItemCount,
            unresolved.BlockedEntryCount
        );
}
