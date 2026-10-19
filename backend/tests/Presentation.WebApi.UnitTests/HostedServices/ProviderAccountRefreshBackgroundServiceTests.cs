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
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Presentation.WebApi.HostedServices;

namespace Presentation.WebApi.UnitTests.HostedServices;

public sealed class ProviderAccountRefreshBackgroundServiceTests
{
    // Cron far past the fixed clock so the scheduled tick never fires unless a test advances time —
    // by default only the startup-if-null and signal-driven paths are exercised.
    private const string NeverDuringTestCron = "0 0 1 1 *";

    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private readonly ISecretProtector _encryption = Substitute.For<ISecretProtector>();
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly IProviderCredentialProvider _credentials =
        Substitute.For<IProviderCredentialProvider>();
    private readonly IWatchedStoreRepository _storeRepo = Substitute.For<IWatchedStoreRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly WatchedStore _store = new(
        Guid.NewGuid(),
        StoreProvider.StreamElements,
        new StoreExternalId("gaules"),
        "Old Gaules",
        Guid.NewGuid(),
        Now
    );
    private readonly IEmailNotifier _email = Substitute.For<IEmailNotifier>();
    private readonly Channel<UsernameRefreshSignal> _channel =
        Channel.CreateUnbounded<UsernameRefreshSignal>();

    // Completes when a refresh persists the user (the terminal step of RefreshProviderAccounts),
    // letting tests await the exact completion of the async refresh instead of polling.
    private readonly TaskCompletionSource _refreshPersisted = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    // The same trick for the store channel-info pass, whose terminal step is the commit.
    private readonly TaskCompletionSource _channelInfoPersisted = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    // Completes when the expiry warning pass sends its email.
    private readonly TaskCompletionSource _expiryWarningSent = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    public ProviderAccountRefreshBackgroundServiceTests()
    {
        _email
            .SendCredentialExpiryAsync(
                Arg.Any<string>(),
                Arg.Any<CredentialExpiryEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _expiryWarningSent.TrySetResult());
        _encryption.Unprotect(Arg.Any<string>()).Returns("plain-token");
        _seApi
            .GetAccountNameAsync("plain-token", Arg.Any<CancellationToken>())
            .Returns("luanholiv");
        _userProvider
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => _refreshPersisted.TrySetResult());
        _storeRepo.ListActiveForUpdateAsync(Arg.Any<CancellationToken>()).Returns([_store]);
        _seApi
            .LookupStoreAsync("gaules", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("channel-id"),
                    "gaules",
                    "Gaules",
                    "https://cdn.example.com/gaules.png"
                )
            );
        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(1)
            .AndDoes(_ => _channelInfoPersisted.TrySetResult());
    }

    private ProviderAccountRefreshBackgroundService BuildService(
        string cronExpression = NeverDuringTestCron
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddSingleton(_userProvider);
        services.AddSingleton(_credentials);
        services.AddScoped<ProviderSecretProtectors>();
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.StreamElementsToken,
            _encryption
        );
        services.AddKeyedSingleton<ISecretProtector>(
            ProtectionPurposes.GauPointsSession,
            Substitute.For<ISecretProtector>()
        );
        services.AddSingleton(StoreProviderClients.FactoryFor(_seApi));
        services.AddSingleton(_storeRepo);
        services.AddSingleton(_unitOfWork);
        services.AddLogging();
        services.AddScoped<RefreshProviderAccounts>();
        services.AddScoped<RefreshStoreChannelInfo>();
        services.AddSingleton(_email);
        services.AddSingleton(Options.Create(new CredentialExpiryOptions()));
        services.AddScoped<WarnExpiringCredentials>();
        var provider = services.BuildServiceProvider();

        return new ProviderAccountRefreshBackgroundService(
            _channel.Reader,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new SyncSettings { UsernameRefreshCronExpression = cronExpression }),
            _timeProvider,
            NullLogger<ProviderAccountRefreshBackgroundService>.Instance
        );
    }

    [Fact]
    public async Task Startup_refresh_runs_when_persisted_username_is_null()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credential = HoldCredential(user, accountName: null);

        var sut = BuildService();
        await sut.StartAsync(CancellationToken.None);

        await _refreshPersisted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await sut.StopAsync(CancellationToken.None);
        credential.AccountName.Should().Be("luanholiv");
    }

    [Fact]
    public async Task Startup_refresh_skipped_when_username_already_present()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credential = HoldCredential(user, accountName: "existing");

        var sut = BuildService();
        await sut.StartAsync(CancellationToken.None);
        await sut.StopAsync(CancellationToken.None);

        // Startup refresh must NOT run when a username is already persisted, and the
        // far-future cron never fires — so the SE API is never queried and the value stands.
        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        credential.AccountName.Should().Be("existing");
    }

    [Fact]
    public async Task Signal_triggers_a_refresh()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credential = HoldCredential(user, accountName: "existing");

        var sut = BuildService();
        await sut.StartAsync(CancellationToken.None);

        _channel.Writer.TryWrite(UsernameRefreshSignal.Instance);

        await _refreshPersisted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await sut.StopAsync(CancellationToken.None);
        credential.AccountName.Should().Be("luanholiv");
    }

    /// <summary>
    /// A refresh pass owns the watched stores' channel display names and avatars too — that data is
    /// not looked up on every store-sync tick, so this schedule is the only thing keeping it
    /// current.
    /// </summary>
    [Fact]
    public async Task Refresh_pass_also_refreshes_store_channel_info()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        HoldCredential(user, accountName: "existing");

        var sut = BuildService();
        await sut.StartAsync(CancellationToken.None);

        _channel.Writer.TryWrite(UsernameRefreshSignal.Instance);

        await _channelInfoPersisted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await sut.StopAsync(CancellationToken.None);
        _store.DisplayName.Should().Be("Gaules");
        _store.AvatarUrl.Should().Be("https://cdn.example.com/gaules.png");
    }

    /// <summary>
    /// The scheduled path, asserted from both sides in one run: short of the cron occurrence no
    /// refresh happens, and crossing it refreshes even without a signal. Proving it eventually fires
    /// keeps the earlier "not yet" assertion from being vacuous, and the virtual clock means the
    /// hour-long gap costs no wall-clock waiting.
    /// </summary>
    [Fact]
    public async Task Cron_refreshes_only_once_its_occurrence_is_reached()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credential = HoldCredential(user, accountName: "existing");

        var sut = BuildService(cronExpression: "0 * * * *"); // hourly, on the hour
        await sut.StartAsync(CancellationToken.None);

        // 15:09 + 30 minutes = 15:39, short of the 16:00 occurrence.
        _timeProvider.Advance(TimeSpan.FromMinutes(30));
        _refreshPersisted.Task.IsCompleted.Should().BeFalse();
        credential.AccountName.Should().Be("existing");

        // 15:39 + 30 minutes = 16:09, past it — throws if the refresh never happens.
        await BackgroundClock.AdvanceUntilSignalledAsync(
            _timeProvider,
            _refreshPersisted.Task,
            TimeSpan.FromMinutes(30)
        );

        await sut.StopAsync(CancellationToken.None);
        credential.AccountName.Should().Be("luanholiv");
    }

    [Fact]
    public async Task Refresh_pass_warns_about_a_credential_about_to_expire()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "owner@example.com" };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credential = HoldCredential(user, accountName: "existing");
        credential.SetExpiresAt(Now.AddHours(12));

        var sut = BuildService();
        await sut.StartAsync(CancellationToken.None);

        _channel.Writer.TryWrite(UsernameRefreshSignal.Instance);

        await _expiryWarningSent.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await sut.StopAsync(CancellationToken.None);
        credential.ExpiryWarningSentFor.Should().Be(Now.AddHours(12));
    }

    /// <summary>
    /// The warning matters most when the provider is failing, so a refresh that throws outright
    /// must not take it down with it.
    /// </summary>
    [Fact]
    public async Task A_failed_refresh_still_warns_about_an_expiring_credential()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "owner@example.com" };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        var credential = HoldCredential(user, accountName: "existing");
        credential.SetExpiresAt(Now.AddHours(12));
        _userProvider
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("identity store down")));

        var sut = BuildService();
        await sut.StartAsync(CancellationToken.None);

        _channel.Writer.TryWrite(UsernameRefreshSignal.Instance);

        await _expiryWarningSent.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await sut.StopAsync(CancellationToken.None);
    }

    /// <summary>The user's StreamElements credential, as the refresh finds it.</summary>
    private ProviderCredential HoldCredential(ApplicationUser user, string? accountName)
    {
        var credential = new ProviderCredential(
            user.Id,
            StoreProvider.StreamElements,
            "encrypted",
            Now
        );
        if (accountName is not null)
            credential.SetAccountName(accountName, Now.AddDays(-1));

        _credentials.ListAsync(user.Id, Arg.Any<CancellationToken>()).Returns([credential]);
        return credential;
    }
}
