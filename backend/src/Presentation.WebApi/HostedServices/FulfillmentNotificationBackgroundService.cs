using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.HostedServices;

/// <summary>
/// Emails gift-card codes to the owner, apart from the submission loop that stored them.
/// </summary>
/// <remarks>
/// A service of its own because SMTP latency is unbounded: sending from the submission loop would
/// let a stalled mail server delay the next redemption, and the submission sweep interval is tuned
/// for redemption retries, not mail.
/// </remarks>
public sealed class FulfillmentNotificationBackgroundService(
    ChannelReader<FulfillmentCodeReady> signal,
    IServiceScopeFactory scopeFactory,
    IOptions<RedemptionsOptions> options,
    TimeProvider timeProvider,
    ILogger<FulfillmentNotificationBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweepInterval = TimeSpan.FromMinutes(options.Value.FulfillmentSweepIntervalMinutes);
        logger.LogFulfillmentSweepScheduled(sweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Drain first: the channel is a wake-up signal, not a work queue — the delivery reads
            // every undelivered code from the database. A restart between a commit and its signal
            // therefore loses nothing: the first sweep after startup finds the row.
            while (signal.TryRead(out _)) { }

            await RunDeliveryAsync(stoppingToken);

            if (
                !await SweepWakeUp.WaitAsync(
                    signal,
                    sweepInterval,
                    timeProvider,
                    logger.LogFulfillmentWakeUpUnavailable,
                    stoppingToken
                )
            )
                return;
        }
    }

    private async Task RunDeliveryAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<DeliverFulfillmentCodes>();
            await useCase.ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Gift-card delivery stopped due to cancellation");
        }
        catch (Exception ex)
        {
            logger.LogFulfillmentCycleFailed(ex);
        }
    }
}
