using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.HostedServices;

namespace Presentation.WebApi.UnitTests.HostedServices;

public sealed class RedemptionPollingBackgroundServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    /// <summary>
    /// Asserted from both sides in one run: short of the cron occurrence nothing is polled, and
    /// crossing it polls. Proving it eventually fires is what keeps the earlier "not yet" assertion
    /// from being vacuous. The clock is virtual, so the hour-long gap costs no wall-clock waiting.
    /// </summary>
    [Fact]
    public async Task Polls_only_once_the_cron_occurrence_is_reached()
    {
        var time = new FakeTimeProvider(Now);
        var (scopeFactory, repo, polled) = SetupWithMockDeps(time);
        var sut = CreateService(scopeFactory, "0 * * * *", time); // hourly, on the hour

        await sut.StartAsync(CancellationToken.None);

        // 15:09 + 30 minutes = 15:39, short of the 16:00 occurrence.
        time.Advance(TimeSpan.FromMinutes(30));
        polled.Task.IsCompleted.Should().BeFalse();
        await repo.DidNotReceive().GetSubmittedForUpdateAsync(Arg.Any<CancellationToken>());

        // 15:39 + 30 minutes = 16:09, past it — throws if the poll never happens.
        await BackgroundClock.AdvanceUntilSignalledAsync(
            time,
            polled.Task,
            TimeSpan.FromMinutes(30)
        );

        await sut.StopAsync(CancellationToken.None);
        await repo.Received().GetSubmittedForUpdateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancellation_stops_polling_service_gracefully()
    {
        var time = new FakeTimeProvider(Now);
        var (scopeFactory, _, _) = SetupWithMockDeps(time);
        var sut = CreateService(scopeFactory, "* * * * *", time);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await cts.CancelAsync();

        var act = () => sut.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    private static RedemptionPollingBackgroundService CreateService(
        IServiceScopeFactory scopeFactory,
        string cronExpression,
        TimeProvider timeProvider
    )
    {
        var opts = Options.Create(
            new RedemptionsOptions { PollingCronExpression = cronExpression }
        );

        return new RedemptionPollingBackgroundService(
            scopeFactory,
            opts,
            timeProvider,
            NullLogger<RedemptionPollingBackgroundService>.Instance
        );
    }

    private static (
        IServiceScopeFactory ScopeFactory,
        IRedemptionRepository Repo,
        TaskCompletionSource Polled
    ) SetupWithMockDeps(TimeProvider timeProvider)
    {
        var polled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var repo = Substitute.For<IRedemptionRepository>();
        repo.GetSubmittedForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                polled.TrySetResult();
                return (IReadOnlyList<Redemption>)[];
            });

        var userProvider = Substitute.For<ISingleUserProvider>();
        userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(timeProvider);
        services.AddSingleton(repo);
        services.AddSingleton(Substitute.For<IStoreProviderClientFactory>());
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.StreamElementsToken,
            Substitute.For<ISecretProtector>()
        );
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.GauPointsSession,
            Substitute.For<ISecretProtector>()
        );
        services.AddSingleton(userProvider);
        services.AddSingleton(Substitute.For<IProviderCredentialProvider>());
        services.AddOptions<RedemptionsOptions>();
        services.AddSingleton(Substitute.For<IUnitOfWork>());
        services.AddScoped<ProviderSecretProtectors>();
        services.AddScoped<ProviderSecrets>();
        services.AddScoped<PollRedemptionStatus>();

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IServiceScopeFactory>(), repo, polled);
    }
}
