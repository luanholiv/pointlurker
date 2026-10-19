namespace PointLurker.Core.Application;

/// <summary>
/// A dependency-scoped <typeparamref name="T"/> together with the scope it was resolved from:
/// disposing this disposes that scope.
/// <para>
/// Injected as <c>Func&lt;ScopedInstance&lt;T&gt;&gt;</c>, it is how application code opens a fresh
/// scope per item of parallel work — one <c>DbContext</c> each — while still declaring what it
/// depends on in its constructor. The alternative, taking <c>IServiceScopeFactory</c> and resolving
/// by type at call time, hides those dependencies from the signature and defers wiring mistakes to
/// run time.
/// </para>
/// </summary>
/// <param name="value">The instance resolved from the scope.</param>
/// <param name="scope">
/// The scope backing <paramref name="value"/>. Tests pass an instance without one; disposal is then
/// a no-op.
/// </param>
public sealed class ScopedInstance<T>(T value, IAsyncDisposable? scope = null) : IAsyncDisposable
    where T : notnull
{
    public T Value { get; } = value;

    public ValueTask DisposeAsync() => scope?.DisposeAsync() ?? ValueTask.CompletedTask;
}
