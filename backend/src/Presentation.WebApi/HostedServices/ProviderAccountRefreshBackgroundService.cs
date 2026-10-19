using System.Threading.Channels;
using Cronos;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.HostedServices;

/// <summary>
/// Keeps every stored provider credential checked and its account name current by invoking
/// <see cref="RefreshProviderAccounts"/> on a cron schedule, on demand via the refresh signal channel
/// (written when a credential is set), and once at startup when some credential has no account
/// name yet.
/// <para>
/// Each scheduled and signalled pass also runs <see cref="RefreshStoreChannelInfo"/>, which
/// refreshes the watched stores' channel display names and avatars. That data belongs on this slow
/// schedule rather than in the sync cycle, where it cost one StreamElements call per store per tick.
/// The startup gap-fill stays account-only.
/// </para>
/// <para>
/// Every pass, the gap-fill included, then runs <see cref="WarnExpiringCredentials"/> on its own,
/// so a refresh that failed outright still leaves the owner warned about a credential running out.
/// </para>
/// </summary>
public sealed class ProviderAccountRefreshBackgroundService(
    ChannelReader<UsernameRefreshSignal> signalReader,
    IServiceScopeFactory scopeFactory,
    IOptions<SyncSettings> settings,
    TimeProvider timeProvider,
    ILogger<ProviderAccountRefreshBackgroundService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cronExpression = settings.Value.UsernameRefreshCronExpression!;
        var cron = CronExpression.Parse(cronExpression);

        logger.LogUsernameRefreshScheduled(cronExpression);

        // Startup gap-fill: only when an account name is missing (persisted values survive restarts).
        if (await IsAccountNameMissingAsync(stoppingToken))
        {
            await RunRefreshAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextOccurrence = cron.GetNextOccurrence(timeProvider.GetUtcNow(), TimeZoneInfo.Utc);
            if (nextOccurrence is null)
            {
                logger.LogUsernameRefreshNoNextOccurrence();
                return;
            }

            bool signalled;
            try
            {
                // Sleep until the next cron occurrence OR a refresh signal — whichever first.
                // Task.Delay caps at ~24.8 days, so long gaps are slept in bounded steps.
                signalled = await WaitForOccurrenceOrSignalAsync(
                    nextOccurrence.Value,
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogUsernameRefreshCancelled();
                return;
            }

            if (stoppingToken.IsCancellationRequested)
                return;

            if (!signalled && timeProvider.GetUtcNow() < nextOccurrence.Value)
            {
                // Spurious wake without reaching the occurrence; recompute and keep waiting.
                continue;
            }

            // Coalesce any queued signals into a single refresh.
            while (signalReader.TryRead(out _)) { }

            await RunRefreshAsync(stoppingToken);
        }
    }

    private static readonly TimeSpan MaxDelayStep = TimeSpan.FromHours(1);

    /// <summary>
    /// Waits until <paramref name="occurrence"/> or a refresh signal arrives. Returns true if a
    /// signal woke it, false if the occurrence was reached.
    /// </summary>
    private async Task<bool> WaitForOccurrenceOrSignalAsync(
        DateTimeOffset occurrence,
        CancellationToken ct
    )
    {
        var signalTask = signalReader.WaitToReadAsync(ct).AsTask();

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var remaining = occurrence - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
                return false;

            var step = remaining < MaxDelayStep ? remaining : MaxDelayStep;
            var completed = await Task.WhenAny(signalTask, Task.Delay(step, timeProvider, ct));
            if (completed == signalTask)
            {
                // Surface cancellation as OCE rather than treating a cancelled wait as a signal.
                await signalTask;
                return true;
            }
        }
    }

    private async Task<bool> IsAccountNameMissingAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var userProvider = scope.ServiceProvider.GetRequiredService<ISingleUserProvider>();
            var user = await userProvider.GetAsync(ct);
            if (user is null)
                return true;

            var credentials =
                scope.ServiceProvider.GetRequiredService<IProviderCredentialProvider>();
            var held = await credentials.ListAsync(user.Id, ct);
            return held.Count == 0
                || held.Any(credential => string.IsNullOrEmpty(credential.AccountName));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task RunRefreshAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope
                .ServiceProvider.GetRequiredService<RefreshProviderAccounts>()
                .ExecuteAsync(ct);
            await scope
                .ServiceProvider.GetRequiredService<RefreshStoreChannelInfo>()
                .ExecuteAsync(ct);
        }
        catch (Exception ex)
        {
            // The refresh use case swallows its own cancellation; anything reaching here is
            // an unexpected failure, never a graceful shutdown.
            logger.LogUsernameRefreshCycleFailed(ex);
        }

        await RunExpiryWarningAsync(ct);
    }

    /// <remarks>
    /// A scope of its own: a failed refresh can leave its context holding changes that never
    /// committed, and the warning's commit must not carry them.
    /// </remarks>
    private async Task RunExpiryWarningAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope
                .ServiceProvider.GetRequiredService<WarnExpiringCredentials>()
                .ExecuteAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-pass: the warning is still owed and the next pass sends it.
            logger.LogCredentialExpiryWarningCancelled();
        }
        catch (Exception ex)
        {
            logger.LogCredentialExpiryWarningPassFailed(ex);
        }
    }
}
