namespace PointLurker.Core.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
