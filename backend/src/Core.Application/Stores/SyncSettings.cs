using System.ComponentModel.DataAnnotations;

namespace PointLurker.Core.Application.Stores;

public sealed class SyncSettings
{
    public const string SectionName = "Sync";

    [Required]
    public string? FetchCronExpression { get; set; } = "* * * * *";

    [Required]
    public string? UsernameRefreshCronExpression { get; set; } = "0 0 * * *";

    [Range(1, 64)]
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    [Range(1, int.MaxValue)]
    public int ResolutionLogRetentionDays { get; set; } = 30;

    /// <summary>
    /// The shortest gap auto-redeem allows between two redemptions of one wishlist entry, as a
    /// floor under the item's own cooldown. An item that declares a longer cooldown keeps it.
    /// <para>
    /// It exists because an item declaring no cooldown otherwise becomes eligible again on the very
    /// next tick, spending against a stock figure the sync has not refreshed yet. Keep it at or
    /// above the fetch cadence for that reason. Zero disables the floor and restores the item's own
    /// cooldown as the only brake on rate.
    /// </para>
    /// </summary>
    [Range(0, 1440)]
    public int MinimumAutoRedeemIntervalMinutes { get; set; } = 5;
}
