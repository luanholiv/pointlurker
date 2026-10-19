namespace PointLurker.Infrastructure.Database;

/// <summary>
/// Change-tracker checks the repositories use to enforce their write contract.
/// <para>
/// <see cref="AppDbContext"/> queries without tracking by default, and write methods persist the
/// change tracker's deltas rather than re-attaching a detached graph. That makes the contract
/// unforgiving in one specific way: handing a write method an entity this context never loaded is not
/// a partial write, it is no write at all, and it looks like success. These helpers turn that into a
/// failure the caller can see.
/// </para>
/// </summary>
internal static class AppDbContextExtensions
{
    /// <summary>
    /// Whether <paramref name="entity"/> is the instance this context is tracking.
    /// <para>
    /// Deliberately goes through <c>ChangeTracker.Entries&lt;T&gt;()</c> rather than
    /// <c>DbContext.Entry(entity)</c>: <c>Entry</c> starts tracking a detached entity as
    /// <c>Unchanged</c>, so using it here would attach the instance, report it as tracked, and then
    /// save nothing — the exact failure this check exists to catch.
    /// </para>
    /// </summary>
    internal static bool IsTrackedForUpdate<TEntity>(this AppDbContext dbContext, TEntity entity)
        where TEntity : class =>
        dbContext
            .ChangeTracker.Entries<TEntity>()
            .Any(entry => ReferenceEquals(entry.Entity, entity));

    /// <summary>
    /// Throws unless <paramref name="entity"/> is tracked, naming the read that would have tracked it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The entity is detached, so saving it is a no-op.</exception>
    internal static void EnsureTrackedForUpdate<TEntity>(
        this AppDbContext dbContext,
        TEntity entity,
        string fetchMethodName
    )
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!dbContext.IsTrackedForUpdate(entity))
        {
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name} is not tracked by this AppDbContext, so saving it would "
                    + $"write nothing. Load it with {fetchMethodName} before mutating it, and keep "
                    + "the load and the save in the same DI scope."
            );
        }
    }
}
