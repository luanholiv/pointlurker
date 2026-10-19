namespace PointLurker.Core.Domain.Repositories;

/// <summary>
/// Optional narrowing applied to the watched-store list; every member is a filter to skip when null.
/// </summary>
/// <param name="TextContains">Case-insensitive substring of either the display name or the channel identifier.</param>
/// <param name="IsActive">Restrict to active stores when <c>true</c>, paused when <c>false</c>; no narrowing when null.</param>
public sealed record WatchedStoreFilter(string? TextContains, bool? IsActive);
