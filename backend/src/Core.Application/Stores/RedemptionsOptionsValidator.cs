using Cronos;
using Microsoft.Extensions.Options;

namespace PointLurker.Core.Application.Stores;

public sealed class RedemptionsOptionsValidator : IValidateOptions<RedemptionsOptions>
{
    public ValidateOptionsResult Validate(string? name, RedemptionsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.PollingCronExpression))
            return ValidateOptionsResult.Fail(
                "RedemptionsOptions: PollingCronExpression is required"
            );

        try
        {
            CronExpression.Parse(options.PollingCronExpression);
        }
        catch (CronFormatException)
        {
            return ValidateOptionsResult.Fail(
                $"RedemptionsOptions: PollingCronExpression '{options.PollingCronExpression}' is not a valid cron expression"
            );
        }

        if (options.CompletionTimeoutHours <= 0)
            return ValidateOptionsResult.Fail(
                $"RedemptionsOptions: CompletionTimeoutHours must be greater than 0 (got {options.CompletionTimeoutHours})"
            );

        if (options.PollFailureAlertThreshold <= 0)
            return ValidateOptionsResult.Fail(
                $"RedemptionsOptions: PollFailureAlertThreshold must be greater than 0 (got {options.PollFailureAlertThreshold})"
            );

        if (options.RetrySweepIntervalMinutes <= 0)
            return ValidateOptionsResult.Fail(
                $"RedemptionsOptions: RetrySweepIntervalMinutes must be greater than 0 (got {options.RetrySweepIntervalMinutes})"
            );

        if (options.FulfillmentSweepIntervalMinutes <= 0)
            return ValidateOptionsResult.Fail(
                $"RedemptionsOptions: FulfillmentSweepIntervalMinutes must be greater than 0 (got {options.FulfillmentSweepIntervalMinutes})"
            );

        return ValidateOptionsResult.Success;
    }
}
