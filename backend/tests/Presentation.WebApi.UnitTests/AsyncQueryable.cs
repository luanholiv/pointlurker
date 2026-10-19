using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Presentation.WebApi.UnitTests;

internal sealed class AsyncQueryable<T>(IEnumerable<T> data) : IQueryable<T>, IAsyncEnumerable<T>
{
    private readonly IQueryable<T> _inner = data.AsQueryable();

    public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

    public Type ElementType => _inner.ElementType;
    public Expression Expression => _inner.Expression;
    public IQueryProvider Provider => new AsyncQueryProvider<T>(_inner.Provider);

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
    {
        foreach (var item in data)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
        await Task.CompletedTask;
    }
}

internal sealed class AsyncQueryProvider<T>(IQueryProvider inner)
    : IQueryProvider,
        IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => inner.CreateQuery(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new AsyncQueryable<TElement>(inner.CreateQuery<TElement>(expression));

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var syncResult = inner.Execute(expression);
        var resultType = typeof(TResult);
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var innerType = resultType.GetGenericArguments()[0];
            var fromResult = typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(innerType)
                .Invoke(null, [syncResult ?? default(T)]);
            return (TResult)fromResult!;
        }
        return (TResult)syncResult!;
    }
}

internal static class AsyncQueryableExtensions
{
    public static IQueryable<T> AsAsyncQueryable<T>(this IEnumerable<T> source) =>
        new AsyncQueryable<T>(source);
}
