using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Presentation.WebApi.HostedServices;

namespace Presentation.WebApi.UnitTests.HostedServices;

public sealed class RedemptionSubmissionBackgroundServiceTests
{
    [Fact]
    public async Task Startup_recovery_invokes_submit_use_case()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(),
            TimeProvider.System,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var called = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                called.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sut.StartAsync(cts.Token);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);

        await repo.Received()
            .GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Channel_message_triggers_submit_use_case()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(),
            TimeProvider.System,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var callCount = 0;
        var startupDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var channelDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 1)
                    startupDone.TrySetResult();
                else
                    channelDone.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await sut.StartAsync(cts.Token);
        await startupDone.Task.WaitAsync(TimeSpan.FromSeconds(3));

        var redemption = CreatePendingRedemption();
        await channel.Writer.WriteAsync(redemption, cts.Token);
        await channelDone.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await sut.StopAsync(CancellationToken.None);

        // Called at least twice: startup + channel message
        await repo.Received(2)
            .GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A transient submission failure schedules a retry for later, but the service is otherwise
    /// woken only by a newly created redemption or a restart. The sweep interval is the floor that
    /// keeps a lone pending row from waiting on an unrelated redemption being created.
    /// </summary>
    [Fact]
    public async Task The_sweep_interval_wakes_the_service_without_a_new_redemption()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero));
        var timeProvider = new TimerSignallingTimeProvider(clock);
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(minutes: 5),
            timeProvider,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var callCount = 0;
        var startupDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var sweepDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var n = Interlocked.Increment(ref callCount);
                if (n == 1)
                    startupDone.TrySetResult();
                else
                    sweepDone.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        await sut.StartAsync(CancellationToken.None);
        await startupDone.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // The sweep timer is armed inside the service's wait, and the callback that released
        // `startupDone` ran midway through the preceding sweep — so the wait does not exist yet.
        // Advancing now would be measured from the new now and never fire, so wait for the arming.
        (await timeProvider.NextTimerArmedAsync())
            .Should()
            .BeTrue();

        // Nothing is written to the channel: only the clock moves.
        clock.Advance(TimeSpan.FromMinutes(5));

        await sweepDone.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await sut.StopAsync(CancellationToken.None);

        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// The channel is a wake-up signal, not a work queue — the sweep re-reads every pending row
    /// regardless. A burst of created redemptions should therefore cost one sweep, not one each.
    /// </summary>
    [Fact]
    public async Task A_burst_of_created_redemptions_does_not_cost_one_sweep_each()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();

        // Queued before the service starts, so the count below is exact rather than a race between
        // the writer and the drain: every message is already waiting when the first pass runs.
        for (var i = 0; i < 20; i++)
            await channel.Writer.WriteAsync(CreatePendingRedemption(), CancellationToken.None);

        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(minutes: 60),
            TimeProvider.System,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var callCount = 0;
        var sweepDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref callCount);
                sweepDone.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        await sut.StartAsync(CancellationToken.None);
        await sweepDone.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);

        // Twenty messages, one sweep. The hour-long interval rules out any other trigger.
        callCount.Should().Be(1);
        channel.Reader.Count.Should().Be(0);
    }

    /// <summary>
    /// A completed channel makes the reader return immediately, forever. Looping straight back to
    /// the sweep would spin the pending query as fast as the database could answer it, so the
    /// service has to fall back to waiting the interval out.
    /// </summary>
    [Fact]
    public async Task A_completed_channel_does_not_spin_the_pending_sweep()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero));
        var timeProvider = new TimerSignallingTimeProvider(clock);
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(minutes: 5),
            timeProvider,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var callCount = 0;
        var startupDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    startupDone.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        await sut.StartAsync(CancellationToken.None);
        await startupDone.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // The first wait, armed on the sweep interval.
        (await timeProvider.NextTimerArmedAsync())
            .Should()
            .BeTrue();

        channel.Writer.Complete();

        // Completing the channel makes the reader return immediately. Arming a second timer is the
        // observable proof that the service chose to wait out the interval rather than loop
        // straight back into the sweep — asserted positively, so nothing here waits on a clock.
        (await timeProvider.NextTimerArmedAsync())
            .Should()
            .BeTrue();
        await sut.StopAsync(CancellationToken.None);

        // The clock never moved, so no armed timer could have fired.
        callCount.Should().Be(1);
    }

    /// <summary>
    /// Waiting out the interval on a completed channel must still lead somewhere: the pending sweep
    /// is the only thing that can retry a redemption whose retry time has since passed, and the
    /// channel can no longer wake anyone.
    /// </summary>
    [Fact]
    public async Task A_completed_channel_still_sweeps_once_the_interval_elapses()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero));
        var timeProvider = new TimerSignallingTimeProvider(clock);
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(minutes: 5),
            timeProvider,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var callCount = 0;
        var startupDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var sweptAgain = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    startupDone.TrySetResult();
                else
                    sweptAgain.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        await sut.StartAsync(CancellationToken.None);
        await startupDone.Task.WaitAsync(TimeSpan.FromSeconds(3));
        (await timeProvider.NextTimerArmedAsync()).Should().BeTrue();

        channel.Writer.Complete();

        // The fallback delay, armed once the reader started returning immediately.
        (await timeProvider.NextTimerArmedAsync())
            .Should()
            .BeTrue();

        clock.Advance(TimeSpan.FromMinutes(5));

        await sweptAgain.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);

        callCount.Should().BeGreaterThanOrEqualTo(2);
    }

    /// <summary>
    /// A channel completed with an error rethrows that error out of the reader — and it is not a
    /// <c>ChannelClosedException</c>, so a catch narrowed to that type lets it escape, retire the
    /// service, and leave redemptions unsubmitted for the life of the process. The channel is only
    /// a wake-up signal, so losing it must cost latency, not the work.
    /// </summary>
    [Fact]
    public async Task A_faulted_channel_keeps_the_service_sweeping()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 14, 15, 9, 26, TimeSpan.Zero));
        var timeProvider = new TimerSignallingTimeProvider(clock);
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(minutes: 5),
            timeProvider,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        var callCount = 0;
        var startupDone = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var sweptAgain = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    startupDone.TrySetResult();
                else
                    sweptAgain.TrySetResult();
                return Task.FromResult<IReadOnlyList<Redemption>>([]);
            });

        await sut.StartAsync(CancellationToken.None);
        await startupDone.Task.WaitAsync(TimeSpan.FromSeconds(3));
        (await timeProvider.NextTimerArmedAsync()).Should().BeTrue();

        channel.Writer.Complete(new InvalidOperationException("channel faulted"));

        // The fallback delay, armed after the fault was absorbed rather than allowed to escape.
        (await timeProvider.NextTimerArmedAsync())
            .Should()
            .BeTrue();

        clock.Advance(TimeSpan.FromMinutes(5));
        await sweptAgain.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Still running, and still submitting: the loop never saw the fault.
        sut.ExecuteTask.Should().NotBeNull();
        sut.ExecuteTask!.IsCompleted.Should().BeFalse();
        callCount.Should().BeGreaterThanOrEqualTo(2);

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Cancellation_stops_service_gracefully()
    {
        var (channel, scopeFactory, repo) = SetupWithMockDeps();
        var sut = new RedemptionSubmissionBackgroundService(
            channel.Reader,
            scopeFactory,
            SweepOptions(),
            TimeProvider.System,
            NullLogger<RedemptionSubmissionBackgroundService>.Instance
        );

        repo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([]);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await sut.StartAsync(cts.Token);

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// A sweep interval long enough that these tests only ever observe the channel-driven and
    /// startup sweeps, never a timer-driven one.
    /// </summary>
    private static IOptions<RedemptionsOptions> SweepOptions(int minutes = 60) =>
        Options.Create(new RedemptionsOptions { RetrySweepIntervalMinutes = minutes });

    private static (
        Channel<Redemption> Channel,
        IServiceScopeFactory ScopeFactory,
        IRedemptionRepository Repo
    ) SetupWithMockDeps()
    {
        var channel = Channel.CreateUnbounded<Redemption>();
        var repo = Substitute.For<IRedemptionRepository>();

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "u@test.com" };
        var userProvider = Substitute.For<ISingleUserProvider>();
        userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credentials = Substitute.For<IProviderCredentialProvider>();
        credentials
            .GetAsync(user.Id, StoreProvider.StreamElements, Arg.Any<CancellationToken>())
            .Returns(
                new ProviderCredential(
                    user.Id,
                    StoreProvider.StreamElements,
                    "enc-token",
                    DateTimeOffset.UtcNow
                )
            );

        var tokenEncryption = Substitute.For<ISecretProtector>();
        tokenEncryption.Unprotect("enc-token").Returns("jwt");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(repo);
        services.AddSingleton(Substitute.For<IStoreProviderClientFactory>());
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.StreamElementsToken,
            tokenEncryption
        );
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.GauPointsSession,
            Substitute.For<ISecretProtector>()
        );
        services.AddSingleton(userProvider);
        services.AddSingleton(credentials);
        services.AddSingleton(Substitute.For<IStoreItemRepository>());
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.StoreInputProfile,
            Substitute.For<ISecretProtector>()
        );
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.RedemptionMessage,
            Substitute.For<ISecretProtector>()
        );
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.FulfillmentCode,
            Substitute.For<ISecretProtector>()
        );
        services.AddSingleton(Substitute.For<IFulfillmentCodeRepository>());
        services.AddSingleton(Channel.CreateUnbounded<FulfillmentCodeReady>().Writer);
        services.AddSingleton<IInputLabelResolver>(
            new FuzzyInputLabelResolver(
                Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
            )
        );
        services.AddSingleton<IStoreInputValueResolver, StoreInputValueResolver>();
        services.AddSingleton(Substitute.For<IUnitOfWork>());
        services.AddScoped<ProviderSecretProtectors>();
        services.AddScoped<ProviderSecrets>();
        services.AddScoped<SubmitRedemptions>();

        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return (channel, scopeFactory, repo);
    }

    private static Redemption CreatePendingRedemption()
    {
        var item = new StoreItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-1"),
                new StoreExternalId("ch-1"),
                "Item",
                null,
                "perk",
                100,
                "default",
                true,
                false,
                true,
                false,
                false,
                [],
                [],
                new ItemQuantity(-1, -1),
                new ItemCooldown(0, 0, 0),
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow
            )
        );
        item.ClearDomainEvents();
        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            item.Id,
            DateTimeOffset.UtcNow
        );
        return entry.CreateRedemption(item, null, "Item", "Store", DateTimeOffset.UtcNow);
    }
}
