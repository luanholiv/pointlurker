using PointLurker.Core.Domain.Common;

namespace PointLurker.Core.Domain.Stores;

public sealed record WishlistEntryInputCoverageRestored(
    Guid WishlistEntryId,
    DateTimeOffset OccurredAt
) : IDomainEvent;
