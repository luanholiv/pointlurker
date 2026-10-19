using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Domain.Repositories;

public interface IFulfillmentCodeRepository
{
    /// <summary>Stages the code for insertion.</summary>
    void Add(FulfillmentCode code);

    /// <summary>
    /// Declares that <paramref name="code"/> is being written, and throws if it is not tracked —
    /// saving a detached entity writes nothing while looking like success. Stages nothing itself.
    /// </summary>
    /// <exception cref="InvalidOperationException">The code came from an untracked read.</exception>
    void Update(FulfillmentCode code);

    /// <summary>
    /// Undelivered codes whose next attempt is due, change-tracked, each with the redemption
    /// snapshot the email names: the caller sends each one and saves the outcome.
    /// </summary>
    Task<IReadOnlyList<PendingFulfillmentDelivery>> GetDueForUpdateAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    );
}
