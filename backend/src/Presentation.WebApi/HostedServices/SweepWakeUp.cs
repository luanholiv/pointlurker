using System.Threading.Channels;

namespace PointLurker.Presentation.WebApi.HostedServices;

/// <summary>
/// The wait between sweeps of a service whose work lives in the database and whose channel only
/// says "look now". Shared so every such service fails the same safe way when the channel does.
/// </summary>
internal static class SweepWakeUp
{
    /// <summary>
    /// Waits for a signal or for the sweep interval to lapse, whichever comes first. Returns
    /// <see langword="false"/> when the service is stopping.
    /// </summary>
    /// <remarks>
    /// The interval is the floor that makes a scheduled retry land: without a timer the only other
    /// wake-ups are a new signal and a process restart, so a lone row due later would otherwise
    /// wait indefinitely.
    /// </remarks>
    /// <param name="onSignalUnavailable">Reports a channel that can no longer wake the service.</param>
    public static async Task<bool> WaitAsync<T>(
        ChannelReader<T> signal,
        TimeSpan sweepInterval,
        TimeProvider timeProvider,
        Action<Exception> onSignalUnavailable,
        CancellationToken stoppingToken
    )
    {
        using var sweepDue = new CancellationTokenSource(sweepInterval, timeProvider);
        using var wake = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            sweepDue.Token
        );

        try
        {
            if (await signal.WaitToReadAsync(wake.Token))
                return true;
        }
        catch (OperationCanceledException)
        {
            // Either the interval lapsed — sweep anyway — or the host is shutting down.
            return !stoppingToken.IsCancellationRequested;
        }
        catch (Exception ex)
        {
            // A channel completed with an error surfaces that error here, and it is not necessarily
            // a ChannelClosedException — completing with any exception rethrows that exception. It
            // must not escape: an unhandled fault here retires the service, and its work would then
            // never run again for the life of the process. The channel is only a wake-up signal,
            // so losing it costs latency, not the work itself.
            onSignalUnavailable(ex);
        }

        // Reached when the channel is finished, cleanly or not: the reader returns immediately from
        // here on, so wait the interval out rather than spinning the sweep as fast as the database
        // can answer it. The sweep still runs, which is what actually does the work.
        try
        {
            await Task.Delay(sweepInterval, timeProvider, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
