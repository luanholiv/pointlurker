using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Presentation.WebApi.HostedServices;

namespace Presentation.WebApi.UnitTests.HostedServices;

public sealed class StoreSyncBackgroundServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    /// <summary>
    /// A bad cron expression fails on the very first statement of the loop, so there is nothing to
    /// wait for — a short yield is enough to prove the service neither crashes the host nor spins.
    /// </summary>
    private static readonly TimeSpan SettleSlice = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task Remains_idle_when_cron_expression_is_missing()
    {
        var sut = CreateService(cronExpression: null);

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(SettleSlice);

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Remains_idle_when_cron_expression_is_empty()
    {
        var sut = CreateService(cronExpression: "");

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(SettleSlice);

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Remains_idle_when_cron_expression_is_invalid()
    {
        var sut = CreateService(cronExpression: "not-a-cron");

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(SettleSlice);

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Respects_cancellation_token_gracefully()
    {
        var time = new FakeTimeProvider(Now);
        var sut = CreateService("* * * * *", time, ScopeFactoryThatSignals(out _));

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await cts.CancelAsync();

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// The scheduled path, asserted from both sides in one run: short of the cron occurrence nothing
    /// happens, and crossing it opens a sync scope. Proving it eventually fires is what makes the
    /// earlier "not yet" assertion meaningful rather than vacuous. The clock is virtual, so the
    /// hour-long gap costs no wall-clock waiting.
    /// </summary>
    [Fact]
    public async Task Starts_a_sync_cycle_only_once_the_cron_occurrence_is_reached()
    {
        var time = new FakeTimeProvider(Now);
        var sut = CreateService("0 * * * *", time, ScopeFactoryThatSignals(out var cycleStarted));

        await sut.StartAsync(CancellationToken.None);

        // 15:09 + 30 minutes = 15:39, short of the 16:00 occurrence.
        time.Advance(TimeSpan.FromMinutes(30));
        cycleStarted.Task.IsCompleted.Should().BeFalse();

        // 15:39 + 30 minutes = 16:09, past it — throws if the cycle never starts.
        await BackgroundClock.AdvanceUntilSignalledAsync(
            time,
            cycleStarted.Task,
            TimeSpan.FromMinutes(30)
        );

        cycleStarted.Task.IsCompleted.Should().BeTrue();
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Signals the moment the loop opens a scope to run a cycle. Resolving <see cref="SyncStores"/>
    /// from the empty scope then throws, which the loop logs and swallows — the signal is what the
    /// scheduling tests care about, so the sync itself needs no wiring.
    /// </summary>
    private static IServiceScopeFactory ScopeFactoryThatSignals(out TaskCompletionSource signal)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal = started;

        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var factory = Substitute.For<IServiceScopeFactory>();
        factory
            .CreateScope()
            .Returns(_ =>
            {
                started.TrySetResult();
                return emptyProvider.CreateScope();
            });

        return factory;
    }

    private static StoreSyncBackgroundService CreateService(
        string? cronExpression,
        TimeProvider? timeProvider = null,
        IServiceScopeFactory? scopeFactory = null
    )
    {
        var settings = Options.Create(new SyncSettings { FetchCronExpression = cronExpression });

        return new StoreSyncBackgroundService(
            // ServiceScopeFactory is unused by the idle tests — the service returns early.
            scopeFactory
                ?? new ServiceCollection()
                    .BuildServiceProvider()
                    .GetRequiredService<IServiceScopeFactory>(),
            settings,
            timeProvider ?? TimeProvider.System,
            NullLogger<StoreSyncBackgroundService>.Instance
        );
    }
}
