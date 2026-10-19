using Cronos;
using Microsoft.Extensions.Options;

namespace PointLurker.Core.Application.Stores;

public sealed class SyncSettingsValidator : IValidateOptions<SyncSettings>
{
    public ValidateOptionsResult Validate(string? name, SyncSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.FetchCronExpression))
            return ValidateOptionsResult.Fail("SyncSettings: FetchCronExpression is required");

        try
        {
            CronExpression.Parse(options.FetchCronExpression);
        }
        catch (CronFormatException)
        {
            return ValidateOptionsResult.Fail(
                $"SyncSettings: FetchCronExpression '{options.FetchCronExpression}' is not a valid cron expression"
            );
        }

        if (options.ResolutionLogRetentionDays < 1)
            return ValidateOptionsResult.Fail(
                $"SyncSettings: ResolutionLogRetentionDays must be at least 1, got {options.ResolutionLogRetentionDays}"
            );

        if (options.MinimumAutoRedeemIntervalMinutes < 0)
            return ValidateOptionsResult.Fail(
                $"SyncSettings: MinimumAutoRedeemIntervalMinutes cannot be negative, got {options.MinimumAutoRedeemIntervalMinutes}"
            );

        if (options.MinimumAutoRedeemIntervalMinutes > 1440)
            return ValidateOptionsResult.Fail(
                $"SyncSettings: MinimumAutoRedeemIntervalMinutes must be at most 1440, got {options.MinimumAutoRedeemIntervalMinutes}"
            );

        if (string.IsNullOrWhiteSpace(options.UsernameRefreshCronExpression))
            return ValidateOptionsResult.Fail(
                "SyncSettings: UsernameRefreshCronExpression is required"
            );

        try
        {
            CronExpression.Parse(options.UsernameRefreshCronExpression);
        }
        catch (CronFormatException)
        {
            return ValidateOptionsResult.Fail(
                $"SyncSettings: UsernameRefreshCronExpression '{options.UsernameRefreshCronExpression}' is not a valid cron expression"
            );
        }

        return ValidateOptionsResult.Success;
    }
}
