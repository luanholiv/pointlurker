namespace Presentation.WebApi.UnitTests.HostedServices;

/// <summary>
/// Reports when the service under test arms a timer, so a test can act only once the wait it
/// means to trip actually exists.
/// </summary>
/// <remarks>
/// The service arms its sweep timer <em>inside</em> its wait, which it reaches after the sweep
/// that precedes it has returned. A test holding only a signal raised during that sweep cannot
/// tell the two apart, and advancing a fake clock too early is measured from the new now and
/// never fires. Wrapping the clock turns "the service is now waiting" into something to await
/// instead of something to sleep through.
/// </remarks>
internal sealed class TimerSignallingTimeProvider(TimeProvider inner) : TimeProvider
{
    private readonly SemaphoreSlim _armed = new(0);

    /// <summary>Completes once one more timer has been armed than has already been observed.</summary>
    public Task<bool> NextTimerArmedAsync() => _armed.WaitAsync(TimeSpan.FromSeconds(3));

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period
    )
    {
        var timer = inner.CreateTimer(callback, state, dueTime, period);
        _armed.Release();
        return timer;
    }

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

    public override long GetTimestamp() => inner.GetTimestamp();

    public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

    public override long TimestampFrequency => inner.TimestampFrequency;
}
