using Cronos;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.HostedServices;

public sealed class RedemptionPollingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RedemptionsOptions> options,
    TimeProvider timeProvider,
    ILogger<RedemptionPollingBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronExpression = options.Value.PollingCronExpression;
        var cron = CronExpression.Parse(cronExpression);

        logger.LogPollingScheduled(cronExpression);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow();
            var next = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (next is null)
                return;

            var delay = next.Value - now;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var useCase = scope.ServiceProvider.GetRequiredService<PollRedemptionStatus>();
                await useCase.ExecuteAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogPollingCycleFailed(ex);
            }
        }
    }
}
