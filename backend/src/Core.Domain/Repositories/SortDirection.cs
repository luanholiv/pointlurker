namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// Which way a searchable list endpoint orders its result. The member names are the values the
/// query string carries (<c>dir=asc</c>, <c>dir=desc</c>), so parsing is an exact name match and
/// there is no translation table to fall out of step with the browser URL.
/// </summary>
public enum SortDirection
{
    Asc,
    Desc,
}
