using PointLurker.Core.Domain.Common;

namespace PointLurker.Core.Domain.Stores;

public sealed record WishlistEntryBlockedOnInput(
    Guid WishlistEntryId,
    IReadOnlyList<string> MissingLabels,
    DateTimeOffset OccurredAt
) : IDomainEvent;
