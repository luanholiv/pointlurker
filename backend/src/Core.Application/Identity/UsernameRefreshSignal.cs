namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Marker message posted to the username-refresh channel to trigger an out-of-schedule
/// refresh (e.g. immediately after a provider credential is set).
/// </summary>
public sealed record UsernameRefreshSignal
{
    public static readonly UsernameRefreshSignal Instance = new();
}
