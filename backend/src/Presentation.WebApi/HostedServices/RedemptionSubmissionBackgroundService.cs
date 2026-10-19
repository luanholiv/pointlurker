using System.Threading.Channels;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Presentation.WebApi.HostedServices;

public sealed class RedemptionSubmissionBackgroundService(
    ChannelReader<Redemption> redemptionChannel,
    IServiceScopeFactory scopeFactory,
    IOptions<RedemptionsOptions> options,
    TimeProvider timeProvider,
    ILogger<RedemptionSubmissionBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweepInterval = TimeSpan.FromMinutes(options.Value.RetrySweepIntervalMinutes);
        logger.LogSubmissionSweepScheduled(sweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Drain first: the channel is a wake-up signal, not a work queue — the sweep below
            // re-reads every pending row from the database anyway. Draining means a burst of newly
            // created redemptions costs one sweep instead of one sweep each.
            while (redemptionChannel.TryRead(out _)) { }

            await RunSubmitAsync(stoppingToken);

            if (
                !await SweepWakeUp.WaitAsync(
                    redemptionChannel,
                    sweepInterval,
                    timeProvider,
                    logger.LogSubmissionWakeUpUnavailable,
                    stoppingToken
                )
            )
                return;
        }
    }

    private async Task RunSubmitAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var useCase = scope.ServiceProvider.GetRequiredService<SubmitRedemptions>();
            await useCase.ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogDebug("Redemption submission stopped due to cancellation");
        }
        catch (Exception ex)
        {
            logger.LogSubmissionCycleFailed(ex);
        }
    }
}
