namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One slice of redemption history plus the unpaged total. The bounds are an offset and a limit
/// rather than a page number and size: the history's numbered pager derives them trivially, and
/// every other list on the API expresses its slice the same way.
/// </summary>
public sealed record RedemptionListResult(
    IReadOnlyList<RedemptionDto> Items,
    int TotalCount,
    int Offset,
    int Limit
);
