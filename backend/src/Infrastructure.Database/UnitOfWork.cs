using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Infrastructure.Database;

/// <summary>
/// The only place <c>SaveChangesAsync</c> is called — enforced by
/// <c>Architecture.Tests/InfrastructureConventionTests</c>, so a repository cannot quietly start
/// committing again.
/// <para>
/// Committing a whole scope at once is safe here precisely because <see cref="AppDbContext"/> tracks
/// only what a <c>*ForUpdateAsync</c> read asked it to: the deltas this flushes can only belong to
/// entities the caller deliberately loaded in order to write them.
/// </para>
/// </summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);
}
