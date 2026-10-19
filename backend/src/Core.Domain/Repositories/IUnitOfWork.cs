namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// The transaction boundary for a write: repositories stage changes, this commits them once.
/// <para>
/// It is scoped alongside the repositories and commits the very context they staged into, so a write
/// path must stage and commit inside one DI scope. Two writes deliberately fall outside it: Identity
/// writes, which go through <c>ISingleUserProvider</c> onto a separate context, and
/// <see cref="IStoreInputResolutionLogRepository.PruneAsync"/>, which issues a set-based delete that
/// bypasses the change tracker and commits on its own.
/// </para>
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Commits every change staged in this scope, returning the number of rows affected.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
