using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace PointLurker.Infrastructure.Database.ValueConverters;

/// <summary>
/// Change-tracking comparer for the list properties mapped by <see cref="JsonListConverter{T}"/>.
/// <para>
/// Every one of those properties is exposed as a read-only view over a mutable backing field, so EF
/// cannot see an edit by reference — it has to compare the collection against a snapshot taken when
/// the entity was loaded. Since writes persist deltas rather than whole rows, dropping the comparer
/// would not produce a visible error: <c>UpsertValue</c>, <c>UpsertAlias</c> and <c>UpdateFromApi</c>
/// would simply stop reaching the database. <c>JsonListComparerTests</c> pins the three behaviours
/// that prevent that — element-wise equality, order sensitivity, and a snapshot that does not alias
/// the live list.
/// </para>
/// </summary>
public static class JsonListComparer
{
    // These three are Expression trees, not lambdas: no `is` patterns and no null-propagation, hence
    // the long-hand null checks.
    public static ValueComparer<IReadOnlyList<T>> Create<T>() =>
        new(
            (left, right) => (left ?? Array.Empty<T>()).SequenceEqual(right ?? Array.Empty<T>()),
            list =>
                list == null
                    ? 0
                    : list.Aggregate(
                        0,
                        (hash, value) =>
                            HashCode.Combine(hash, value == null ? 0 : value.GetHashCode())
                    ),
            list => list == null ? Array.Empty<T>() : list.ToArray()
        );
}
