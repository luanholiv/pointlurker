using Core.Application.UnitTests.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class PollRedemptionStatusTests
{
    private readonly IRedemptionRepository _redemptionRepo =
        Substitute.For<IRedemptionRepository>();
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private readonly ISecretProtector _tokenEncryption = Substitute.For<ISecretProtector>();
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly PollRedemptionStatus _sut;

    public PollRedemptionStatusTests()
    {
        var opts = Options.Create(new RedemptionsOptions { CompletionTimeoutHours = 24 });

        _sut = new PollRedemptionStatus(
            _redemptionRepo,
            StoreProviderClients.FactoryFor(_seApi),
            new ProviderSecrets(
                _credentials,
                new ProviderSecretProtectors(_tokenEncryption, Substitute.For<ISecretProtector>()),
                _timeProvider
            ),
            _userProvider,
            _unitOfWork,
            opts,
            _timeProvider,
            NullLogger<PollRedemptionStatus>.Instance
        );
    }

    [Fact]
    public async Task Submitted_transitions_to_Completed_when_SE_returns_completed_true()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        var completedAt = Now;
        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SeLookup(redemption, completed: true, updatedAt: completedAt));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Completed);
        redemption.SettledAt.Should().Be(completedAt);
        _redemptionRepo
            .Received(1)
            .Update(Arg.Is<Redemption>(r => r.Status == RedemptionStatus.Completed));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submitted_remains_Submitted_when_SE_returns_completed_false()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SeLookup(redemption, completed: false, updatedAt: Now));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        _redemptionRepo.DidNotReceive().Update(Arg.Any<Redemption>());
        // Nothing finished upstream, so the sweep commits nothing — the common case on a timer tick.
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submitted_transitions_to_CompletionTimedOut_when_past_timeout()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-25));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
        _redemptionRepo
            .Received(1)
            .Update(Arg.Is<Redemption>(r => r.Status == RedemptionStatus.CompletionTimedOut));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _seApi
            .DidNotReceive()
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>
    /// A failing status check leaves the status alone — the submission did succeed — but is
    /// counted, which is what tells a permanent break apart from work still pending before the
    /// completion timeout expires.
    /// </summary>
    [Fact]
    public async Task SE_API_error_is_counted_against_the_redemption_and_the_status_is_untouched()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("SE API down"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.FailureCode.Should().BeNull();
        redemption.PollFailures.Should().Be(1);
        redemption.LastPollError.Should().Contain("SE API down");
        _redemptionRepo.Received(1).Update(redemption);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consecutive_failed_status_checks_accumulate_on_the_redemption()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("SE API down"));

        await _sut.ExecuteAsync();
        await _sut.ExecuteAsync();
        await _sut.ExecuteAsync();

        redemption.PollFailures.Should().Be(3);
        redemption.Status.Should().Be(RedemptionStatus.Submitted);
    }

    /// <summary>
    /// Crossing the configured threshold escalates the log from a per-tick warning to an error.
    /// A per-tick warning is what a permanently broken status check looked like for a whole day,
    /// which is precisely how it went unnoticed.
    /// </summary>
    [Fact]
    public async Task Crossing_the_alert_threshold_escalates_without_touching_the_redemption()
    {
        var logger = new RecordingLogger<PollRedemptionStatus>();
        var sut = new PollRedemptionStatus(
            _redemptionRepo,
            StoreProviderClients.FactoryFor(_seApi),
            new ProviderSecrets(
                _credentials,
                new ProviderSecretProtectors(_tokenEncryption, Substitute.For<ISecretProtector>()),
                _timeProvider
            ),
            _userProvider,
            _unitOfWork,
            Options.Create(
                new RedemptionsOptions
                {
                    CompletionTimeoutHours = 24,
                    PollFailureAlertThreshold = 2,
                }
            ),
            _timeProvider,
            logger
        );

        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);
        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("SE API down"));

        await sut.ExecuteAsync();
        logger.Errors.Should().BeEmpty();

        await sut.ExecuteAsync();

        logger.Errors.Should().ContainSingle();
        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.PollFailures.Should().Be(2);
    }

    /// <summary>
    /// Shutdown is not an upstream fault. Recording it would inflate the failure count on every
    /// restart and eventually escalate a perfectly healthy redemption.
    /// </summary>
    [Fact]
    public async Task A_cancelled_sweep_is_not_counted_as_a_failed_status_check()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Matched on any token: the sweep is invoked with the cancelled one, and a stub recorded
        // against the default token would simply not match, leaving the sweep with nothing to do.
        _redemptionRepo
            .GetSubmittedForUpdateAsync(Arg.Any<CancellationToken>())
            .Returns([redemption]);

        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => _sut.ExecuteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        redemption.PollFailures.Should().Be(0);
        redemption.LastPollError.Should().BeNull();
        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A refusal arrives as <c>rejected: true</c> with <c>completed: false</c>. Read without the
    /// flag it looks pending, so the row waited out the completion timeout under the wrong status.
    /// </summary>
    [Fact]
    public async Task Submitted_transitions_to_Rejected_when_SE_reports_a_rejection()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        var rejectedAt = Now;
        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SeLookup(redemption, completed: false, updatedAt: rejectedAt, rejected: true));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Rejected);
        redemption.SettledAt.Should().Be(rejectedAt);
        _redemptionRepo
            .Received(1)
            .Update(Arg.Is<Redemption>(r => r.Status == RedemptionStatus.Rejected));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The timeout sweep runs ahead of the token checks: a missing or unreadable token must not
    /// freeze outstanding redemptions in Submitted, because a Submitted row blocks auto-redeem for
    /// its wishlist entry.
    /// </summary>
    [Fact]
    public async Task An_overdue_redemption_still_times_out_when_no_user_exists()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-25));
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_overdue_redemption_still_times_out_when_no_token_is_configured()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-25));
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_overdue_redemption_still_times_out_when_the_token_will_not_decrypt()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-25));
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        _credentials.With(
            new ProviderCredential(user.Id, StoreProvider.StreamElements, "encrypted-token", Now)
        );
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        _tokenEncryption
            .Unprotect("encrypted-token")
            .Throws(new InvalidOperationException("key ring gone"));
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A row that is not yet overdue cannot be read without a token, but must not be charged a
    /// poll failure for our own missing credential either.
    /// </summary>
    [Fact]
    public async Task A_current_redemption_is_left_alone_when_no_token_is_available()
    {
        var redemption = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([redemption]);

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.PollFailures.Should().Be(0);
        _redemptionRepo.DidNotReceive().Update(Arg.Any<Redemption>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _seApi
            .DidNotReceive()
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>
    /// A provider that cannot read a redemption back is never asked to, and its credential is not
    /// even decrypted for it. Its rows still resolve on the completion timeout.
    /// </summary>
    [Fact]
    public async Task A_provider_without_redemption_polling_is_not_read_back()
    {
        var current = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        var overdue = CreateSubmittedRedemption(submittedAt: Now.AddHours(-25));
        SetupUser();
        _seApi.Capabilities.Returns(
            StoreProviderClients.StreamElementsLike with
            {
                SupportsRedemptionPolling = false,
            }
        );
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([current, overdue]);

        await _sut.ExecuteAsync();

        current.Status.Should().Be(RedemptionStatus.Submitted);
        current.PollFailures.Should().Be(0);
        overdue.Status.Should().Be(RedemptionStatus.CompletionTimedOut);
        _tokenEncryption.DidNotReceive().Unprotect(Arg.Any<string>());
        await _seApi
            .DidNotReceive()
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task The_credential_is_decrypted_once_per_sweep_not_once_per_row()
    {
        var first = CreateSubmittedRedemption(submittedAt: Now.AddHours(-1));
        var second = CreateSubmittedRedemption(submittedAt: Now.AddHours(-2));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([first, second]);
        _seApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProviderRedemptionStatus(false, false, Now));

        await _sut.ExecuteAsync();

        _tokenEncryption.Received(1).Unprotect("encrypted-token");
        await _seApi
            .Received(2)
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task A_sweep_over_several_finished_redemptions_commits_once()
    {
        var first = CreateSubmittedRedemption(submittedAt: Now.AddHours(-25));
        var second = CreateSubmittedRedemption(submittedAt: Now.AddHours(-26));
        var third = CreateSubmittedRedemption(submittedAt: Now.AddHours(-27));
        SetupUser();
        _redemptionRepo.GetSubmittedForUpdateAsync().Returns([first, second, third]);

        await _sut.ExecuteAsync();

        _redemptionRepo.Received(3).Update(Arg.Any<Redemption>());
        // Three rows, one transaction — not one commit per redemption.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Captures log levels so a test can assert the escalation from warning to error, which is the
    /// only outward difference the alert threshold makes.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<LogLevel> _entries = [];

        public IEnumerable<LogLevel> Errors => _entries.Where(level => level == LogLevel.Error);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => _entries.Add(logLevel);
    }

    private static ProviderRedemptionStatus SeLookup(
        Redemption redemption,
        bool completed,
        DateTimeOffset updatedAt,
        bool rejected = false
    ) => new(completed, rejected, updatedAt);

    private void SetupUser()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        _credentials.With(
            new ProviderCredential(user.Id, StoreProvider.StreamElements, "encrypted-token", Now)
        );
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        _tokenEncryption.Unprotect("encrypted-token").Returns("decrypted-jwt");
    }

    private static Redemption CreateSubmittedRedemption(DateTimeOffset submittedAt)
    {
        var item = new StoreItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-item-1"),
                new StoreExternalId("ch-1"),
                "Test Item",
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
                Now,
                null,
                Now
            )
        );
        item.ClearDomainEvents();

        var entry = new WishlistEntry(Guid.NewGuid(), Guid.NewGuid(), item.Id, Now);
        var redemption = entry.CreateRedemption(item, null, "Item", "Store", Now);
        redemption.MarkSubmitted("se-redemption-id-1", submittedAt);
        return redemption;
    }
}
