using Cronos;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.HostedServices;

public sealed class StoreSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncSettings> settings,
    TimeProvider timeProvider,
    ILogger<StoreSyncBackgroundService> logger
) : BackgroundService
{
    private int _running;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronExpression = settings.Value.FetchCronExpression!;
        var cron = CronExpression.Parse(cronExpression);

        logger.LogSyncScheduled(cronExpression);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = timeProvider.GetUtcNow();
            var nextOccurrence = cron.GetNextOccurrence(now, TimeZoneInfo.Utc);

            if (nextOccurrence is null)
            {
                logger.LogNoNextOccurrence();
                return;
            }

            var delay = nextOccurrence.Value - now;
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

            // Overlap prevention
            if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                logger.LogSyncAlreadyRunning();
                continue;
            }

            try
            {
                logger.LogSyncCycleStarting();
                await using var scope = scopeFactory.CreateAsyncScope();
                var syncStores = scope.ServiceProvider.GetRequiredService<SyncStores>();
                var result = await syncStores.ExecuteAsync(stoppingToken);
                logger.LogSyncCycleCompleted(
                    result.StoresSynced,
                    result.StoresSkipped,
                    result.ItemsChanged,
                    result.ItemsProcessed,
                    result.RedemptionsAttempted,
                    result.RedemptionsPending,
                    result.NotificationsSent
                );
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogSyncCycleFailed(ex);
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }
    }
}
