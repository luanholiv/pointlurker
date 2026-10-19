using System.Threading.Channels;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
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
using PointLurker.Presentation.WebApi.HostedServices;

namespace Presentation.WebApi.UnitTests.HostedServices;

public sealed class FulfillmentNotificationBackgroundServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly Channel<FulfillmentCodeReady> _signal =
        Channel.CreateUnbounded<FulfillmentCodeReady>();
    private readonly IFulfillmentCodeRepository _codes =
        Substitute.For<IFulfillmentCodeRepository>();

    private int _sweeps;
    private readonly TaskCompletionSource _firstSweep = NewSignal();
    private readonly TaskCompletionSource _secondSweep = NewSignal();

    public FulfillmentNotificationBackgroundServiceTests()
    {
        _codes
            .GetDueForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var n = Interlocked.Increment(ref _sweeps);
                (n == 1 ? _firstSweep : _secondSweep).TrySetResult();
                return Task.FromResult<IReadOnlyList<PendingFulfillmentDelivery>>([]);
            });
    }

    /// <summary>
    /// The database is the queue, so a code stored just before a restart is found by the first
    /// sweep with no signal at all.
    /// </summary>
    [Fact]
    public async Task Startup_sweeps_without_waiting_for_a_signal()
    {
        var sut = Service(TimeProvider.System);

        await sut.StartAsync(CancellationToken.None);
        await _firstSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_stored_code_wakes_the_delivery()
    {
        var sut = Service(TimeProvider.System);

        await sut.StartAsync(CancellationToken.None);
        await _firstSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await _signal.Writer.WriteAsync(FulfillmentCodeReady.Instance);
        await _secondSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// A failed send is retried at a later time, and only the sweep can land that retry: nothing
    /// else wakes the service until another code is stored.
    /// </summary>
    [Fact]
    public async Task The_sweep_interval_wakes_the_delivery_without_a_signal()
    {
        var clock = new FakeTimeProvider(Start);
        var timeProvider = new TimerSignallingTimeProvider(clock);
        var sut = Service(timeProvider, sweepMinutes: 5);

        await sut.StartAsync(CancellationToken.None);
        await _firstSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        (await timeProvider.NextTimerArmedAsync()).Should().BeTrue();

        clock.Advance(TimeSpan.FromMinutes(5));

        await _secondSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_failed_cycle_does_not_retire_the_service()
    {
        var calls = 0;
        _codes
            .GetDueForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    _firstSweep.TrySetResult();
                    throw new InvalidOperationException("database down");
                }
                _secondSweep.TrySetResult();
                return Task.FromResult<IReadOnlyList<PendingFulfillmentDelivery>>([]);
            });
        var sut = Service(TimeProvider.System);

        await sut.StartAsync(CancellationToken.None);
        // Signalled only once the failing sweep is under way: the loop drains the channel before
        // each sweep, so a signal written earlier would be discarded with nothing left to wake it.
        await _firstSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await _signal.Writer.WriteAsync(FulfillmentCodeReady.Instance);
        await _secondSweep.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await sut.StopAsync(CancellationToken.None);
    }

    private FulfillmentNotificationBackgroundService Service(
        TimeProvider timeProvider,
        int sweepMinutes = 60
    )
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "owner@example.com" };
        var userProvider = Substitute.For<ISingleUserProvider>();
        userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(timeProvider);
        services.AddSingleton(_codes);
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.FulfillmentCode,
            Substitute.For<ISecretProtector>()
        );
        services.AddSingleton(Substitute.For<IEmailNotifier>());
        services.AddSingleton(Substitute.For<INotificationRepository>());
        services.AddSingleton(userProvider);
        services.AddSingleton(Substitute.For<IUnitOfWork>());
        services.AddScoped<DeliverFulfillmentCodes>();

        return new FulfillmentNotificationBackgroundService(
            _signal.Reader,
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(
                new RedemptionsOptions { FulfillmentSweepIntervalMinutes = sweepMinutes }
            ),
            timeProvider,
            NullLogger<FulfillmentNotificationBackgroundService>.Instance
        );
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
