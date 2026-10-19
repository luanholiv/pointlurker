using Microsoft.Extensions.Time.Testing;

namespace Presentation.WebApi.UnitTests.HostedServices;

/// <summary>
/// Drives a <see cref="FakeTimeProvider"/> forward for hosted services that sleep on
/// <c>Task.Delay(delay, timeProvider, ct)</c>, so cron schedules can be asserted without any
/// wall-clock waiting.
/// </summary>
internal static class BackgroundClock
{
    /// <summary>Bounded attempts, so a service that never fires fails instead of hanging.</summary>
    private const int DefaultAttempts = 40;

    /// <summary>How long each attempt waits on the signal before advancing the clock again.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Advances the virtual clock by <paramref name="advanceBy"/> until <paramref name="signal"/>
    /// completes, throwing a descriptive <see cref="TimeoutException"/> if it never does.
    /// </summary>
    /// <remarks>
    /// <see cref="FakeTimeProvider.Advance"/> only fires timers already registered, and a hosted
    /// service registers its delay on a background task the test cannot await. Retrying the advance
    /// converges regardless of when registration lands: each attempt waits on the signal itself — a
    /// <see cref="TaskCompletionSource"/> completed by the work under test — rather than sleeping for
    /// a guessed duration, and returns the instant it fires.
    /// </remarks>
    public static async Task AdvanceUntilSignalledAsync(
        FakeTimeProvider time,
        Task signal,
        TimeSpan advanceBy,
        int attempts = DefaultAttempts
    )
    {
        var advanced = TimeSpan.Zero;
        TimeoutException? lastTimeout = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            time.Advance(advanceBy);
            advanced += advanceBy;

            try
            {
                await signal.WaitAsync(AttemptTimeout);
                return;
            }
            catch (TimeoutException ex)
            {
                // Nothing was due yet, or the service had not registered its delay when the clock
                // moved. Keep the last timeout as the inner cause of the failure reported below.
                lastTimeout = ex;
            }
        }

        throw new TimeoutException(
            $"The signal never fired after advancing the virtual clock by {advanced} "
                + $"across {attempts} attempts.",
            lastTimeout
        );
    }
}
