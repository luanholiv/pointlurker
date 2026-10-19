using System.Threading.Channels;
using Core.Application.UnitTests.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class SubmitRedemptionsTests
{
    private readonly IRedemptionRepository _redemptionRepo =
        Substitute.For<IRedemptionRepository>();
    private readonly IStoreItemRepository _storeItemRepo = Substitute.For<IStoreItemRepository>();
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private readonly ISecretProtector _tokenEncryption = Substitute.For<ISecretProtector>();
    private readonly ISecretProtector _messageProtector = Substitute.For<ISecretProtector>();
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly InMemoryProviderCredentials _credentials = new();
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFulfillmentCodeRepository _codeRepo =
        Substitute.For<IFulfillmentCodeRepository>();
    private readonly ISecretProtector _codeProtector = Substitute.For<ISecretProtector>();
    private readonly Channel<FulfillmentCodeReady> _codeSignal =
        Channel.CreateUnbounded<FulfillmentCodeReady>();
    private readonly SubmitRedemptions _sut;

    public SubmitRedemptionsTests()
    {
        _sut = new SubmitRedemptions(
            _redemptionRepo,
            _storeItemRepo,
            new StoreInputValueResolver(_resolver, _protector),
            StoreProviderClients.FactoryFor(_seApi),
            new ProviderSecrets(
                _credentials,
                new ProviderSecretProtectors(_tokenEncryption, Substitute.For<ISecretProtector>()),
                _timeProvider
            ),
            _messageProtector,
            _codeRepo,
            _codeProtector,
            _codeSignal.Writer,
            _userProvider,
            _unitOfWork,
            _timeProvider,
            NullLogger<SubmitRedemptions>.Instance
        );
    }

    [Fact]
    public async Task Pending_transitions_to_Submitted_on_successful_SE_call()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(CreateSeRedemption("se-redemption-1", completed: false));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Submitted);
        redemption.ProviderRedemptionId.Should().Be("se-redemption-1");
        _redemptionRepo
            .Received(1)
            .Update(Arg.Is<Redemption>(r => r.Status == RedemptionStatus.Submitted));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unprotects_message_snapshot_and_sends_plaintext_to_SE()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1", message: "ct:thanks");
        SetupUser();
        _messageProtector.Unprotect("ct:thanks").Returns("thanks for the sub!");
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(CreateSeRedemption("se-redemption-1", completed: false));

        await _sut.ExecuteAsync();

        _messageProtector.Received(1).Unprotect("ct:thanks");
        await _seApi
            .Received(1)
            .RedeemAsync(
                Arg.Is<ProviderRedeemCommand>(c => c.Message == "thanks for the sub!"),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sends_null_message_without_unprotecting()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1", message: null);
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(CreateSeRedemption("se-redemption-1", completed: false));

        await _sut.ExecuteAsync();

        _messageProtector.DidNotReceive().Unprotect(Arg.Any<string>());
        await _seApi
            .Received(1)
            .RedeemAsync(
                Arg.Is<ProviderRedeemCommand>(c => c.Message == null),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    /// <summary>
    /// The durable row id travels with the command, so a provider that de-duplicates recognises a
    /// retry hours later as the same redemption.
    /// </summary>
    [Fact]
    public async Task Sends_the_redemption_id_as_the_idempotency_key()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(CreateSeRedemption("se-redemption-1", completed: false));

        await _sut.ExecuteAsync();

        await _seApi
            .Received(1)
            .RedeemAsync(
                Arg.Is<ProviderRedeemCommand>(c => c.RedemptionId == redemption.Id),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task A_provider_that_accepts_without_an_id_completes_the_redemption_in_one_pass()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ProviderRedemptionResult.Accepted(null, Completed: true, FulfillmentCode: null)
            );

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Completed);
        redemption.ProviderRedemptionId.Should().BeNull();
    }

    [Fact]
    public async Task A_refusal_of_the_provider_own_fails_the_redemption_permanently()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProviderRedemptionResult.Refused("HTTP 400: Item inválido."));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderRejected);
        redemption.FailureContext.Should().Be("HTTP 400: Item inválido.");
        redemption.NextRetryAt.Should().BeNull();
    }

    [Fact]
    public async Task An_expired_credential_fails_its_redemptions_without_calling_the_provider()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _credentials.Held.Single().SetExpiresAt(Now.AddMinutes(-1));
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.CredentialExpired);
        redemption.FailureContext.Should().StartWith("StreamElements credential expired at ");
        _tokenEncryption.DidNotReceive().Unprotect(Arg.Any<string>());
        await _seApi
            .DidNotReceive()
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Pending_transitions_to_Completed_when_SE_returns_completed_true()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(CreateSeRedemption("se-redemption-1", completed: true));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Completed);
    }

    [Fact]
    public async Task Pending_transitions_to_SubmissionFailed_on_401_error()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ProviderApiException(401, "Unauthorized", "Unauthorized"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderApiUnauthorized);
    }

    [Fact]
    public async Task Pending_transitions_to_SubmissionFailed_on_400_error()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ProviderApiException(400, "Bad Request", "Bad Request"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderApiBadRequest);
    }

    [Fact]
    public async Task Pending_fails_permanently_when_the_provider_reports_too_few_points()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProviderRedemptionResult.InsufficientPoints("HTTP 400: not enough"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.InsufficientPoints);
        redemption.FailureContext.Should().Be("HTTP 400: not enough");
        redemption.NextRetryAt.Should().BeNull();
    }

    /// <summary>
    /// The credential gate is per provider. A redemption for a provider the owner holds no
    /// credential for fails on its own, and the rest of the sweep still reaches its provider.
    /// </summary>
    [Fact]
    public async Task A_provider_without_a_credential_fails_only_its_own_redemptions()
    {
        var streamElements = CreatePendingRedemption("ch-1", "se-item-1");
        var gauPoints = CreatePendingRedemption(
            "gaupoints",
            "gift-card",
            provider: StoreProvider.GauPoints
        );
        SetupUser();
        _redemptionRepo
            .GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>())
            .Returns([gauPoints, streamElements]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(CreateSeRedemption("se-redemption-1", completed: false));

        await _sut.ExecuteAsync();

        streamElements.Status.Should().Be(RedemptionStatus.Submitted);
        gauPoints.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        gauPoints.FailureCode.Should().Be(SubmissionFailureCode.TokenDecryptionFailed);
        gauPoints.FailureContext.Should().Be("GauPoints credential not configured");
        await _seApi
            .Received(1)
            .RedeemAsync(
                Arg.Is<ProviderRedeemCommand>(c => c.Item == new ProviderItemId("se-item-1")),
                "decrypted-jwt",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Pending_transitions_to_SubmissionFailed_on_404_error()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ProviderApiException(404, "Not Found", "Not Found"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderApiNotFound);
    }

    [Fact]
    public async Task Pending_transitions_to_SubmissionFailed_on_5xx_error()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new ProviderApiException(500, "Internal Server Error", "Server error"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        redemption.FailureCode.Should().Be(SubmissionFailureCode.ProviderApiServerError);
    }

    [Fact]
    public async Task Pending_schedules_retry_on_transient_network_error()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Pending);
        redemption.NextRetryAt.Should().NotBeNull();
        redemption.Attempts.Should().Be(1);
    }

    /// <summary>
    /// Backoff is <c>now + 2^(attempts + 1) seconds + jitter</c>, jitter being a random fraction of
    /// a second. With the clock fixed, the window the retry must land in is exact — the first
    /// attempt is due between 2s and 3s after now.
    /// </summary>
    [Fact]
    public async Task Retry_backoff_lands_in_the_expected_window_for_the_first_attempt()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await _sut.ExecuteAsync();

        redemption
            .NextRetryAt!.Value.Should()
            .BeOnOrAfter(Now.AddSeconds(2))
            .And.BeBefore(Now.AddSeconds(3));
    }

    /// <summary>
    /// The window doubles per attempt: a redemption that already failed twice is due between 8s and
    /// 9s after now (<c>2^3</c> plus jitter).
    /// </summary>
    [Fact]
    public async Task Retry_backoff_grows_exponentially_with_attempts()
    {
        var redemption = CreatePendingRedemption("ch-1", "se-item-1");
        redemption.ScheduleRetry(Now.AddSeconds(-30));
        redemption.ScheduleRetry(Now.AddSeconds(-15));
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        await _sut.ExecuteAsync();

        redemption
            .NextRetryAt!.Value.Should()
            .BeOnOrAfter(Now.AddSeconds(8))
            .And.BeBefore(Now.AddSeconds(9));
    }

    /// <summary>
    /// The pending sweep is bounded by the clock, so a redemption whose retry is still in the
    /// future is simply not returned — the query cutoff must be the injected "now".
    /// </summary>
    [Fact]
    public async Task Pending_sweep_queries_with_the_injected_clock()
    {
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([]);

        await _sut.ExecuteAsync();

        await _redemptionRepo
            .Received(1)
            .GetPendingForUpdateAsync(Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Token_decryption_failure_marks_all_pending_as_SubmissionFailed()
    {
        var r1 = CreatePendingRedemption("ch-1", "se-item-1");
        var r2 = CreatePendingRedemption("ch-2", "se-item-2");
        SetupUser();
        _tokenEncryption.Unprotect(Arg.Any<string>()).Throws<InvalidOperationException>();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([r1, r2]);

        await _sut.ExecuteAsync();

        r1.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        r2.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        r1.FailureCode.Should().Be(SubmissionFailureCode.TokenDecryptionFailed);
        r2.FailureCode.Should().Be(SubmissionFailureCode.TokenDecryptionFailed);
        // The cause is shared by the whole batch, so the outcome is one transaction, not one each.
        _redemptionRepo.Received(2).Update(Arg.Any<Redemption>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _seApi
            .DidNotReceive()
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Skips_when_no_pending_redemptions()
    {
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([]);

        await _sut.ExecuteAsync();

        await _seApi
            .DidNotReceive()
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Skips_entirely_when_no_user_exists()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        await _sut.ExecuteAsync();

        await _redemptionRepo
            .DidNotReceive()
            .GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task No_token_configured_marks_all_pending_as_failed()
    {
        var r1 = CreatePendingRedemption("ch-1", "se-item-1");
        var r2 = CreatePendingRedemption("ch-2", "se-item-2");

        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([r1, r2]);

        await _sut.ExecuteAsync();

        r1.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        r2.Status.Should().Be(RedemptionStatus.SubmissionFailed);
        r1.FailureCode.Should().Be(SubmissionFailureCode.TokenDecryptionFailed);
        r2.FailureCode.Should().Be(SubmissionFailureCode.TokenDecryptionFailed);
        _redemptionRepo.Received(2).Update(Arg.Any<Redemption>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _seApi
            .DidNotReceive()
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Each_submitted_redemption_is_committed_before_the_next_is_attempted()
    {
        // Per redemption, deliberately: the StreamElements call has already happened, so its outcome
        // has to be durable before the next submission starts.
        var r1 = CreatePendingRedemption("ch-1", "se-item-1");
        var r2 = CreatePendingRedemption("ch-2", "se-item-2");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([r1, r2]);
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ => CreateSeRedemption($"se-red-{Guid.NewGuid():N}", completed: false));

        await _sut.ExecuteAsync();

        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_returned_code_is_encrypted_and_committed_with_the_completion()
    {
        var redemption = CreatePendingRedemption("gaupoints", "gp-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _codeProtector.Protect("GIFT-1234").Returns("ct:gift");
        StubAccepted(fulfillmentCode: "GIFT-1234");

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Completed);
        Received.InOrder(() =>
        {
            _codeRepo.Add(
                Arg.Is<FulfillmentCode>(code =>
                    code.RedemptionId == redemption.Id
                    && code.EncryptedCode == "ct:gift"
                    && code.CreatedAt == Now
                    && code.SentAt == null
                )
            );
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_committed_code_wakes_the_delivery()
    {
        var redemption = CreatePendingRedemption("gaupoints", "gp-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _codeProtector.Protect("GIFT-1234").Returns("ct:gift");
        StubAccepted(fulfillmentCode: "GIFT-1234");

        await _sut.ExecuteAsync();

        _codeSignal.Reader.TryRead(out _).Should().BeTrue();
    }

    [Fact]
    public async Task An_answer_without_a_code_stages_none_and_signals_nothing()
    {
        var redemption = CreatePendingRedemption("gaupoints", "gp-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        StubAccepted(fulfillmentCode: null);

        await _sut.ExecuteAsync();

        _codeRepo.DidNotReceive().Add(Arg.Any<FulfillmentCode>());
        _codeProtector.DidNotReceive().Protect(Arg.Any<string>());
        _codeSignal.Reader.TryRead(out _).Should().BeFalse();
    }

    /// <summary>
    /// The points are spent by the time the code arrives. Retrying would spend them again, so a
    /// code that cannot be encrypted is lost loudly rather than resubmitted.
    /// </summary>
    [Fact]
    public async Task A_code_that_cannot_be_encrypted_still_completes_the_redemption()
    {
        var redemption = CreatePendingRedemption("gaupoints", "gp-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _codeProtector
            .Protect(Arg.Any<string>())
            .Throws(new InvalidOperationException("key ring unavailable"));
        StubAccepted(fulfillmentCode: "GIFT-1234");

        await _sut.ExecuteAsync();

        redemption.Status.Should().Be(RedemptionStatus.Completed);
        redemption.NextRetryAt.Should().BeNull();
        _codeRepo.DidNotReceive().Add(Arg.Any<FulfillmentCode>());
        _codeSignal.Reader.TryRead(out _).Should().BeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_code_whose_commit_fails_signals_nothing()
    {
        var redemption = CreatePendingRedemption("gaupoints", "gp-item-1");
        SetupUser();
        _redemptionRepo.GetPendingForUpdateAsync(Arg.Any<DateTimeOffset>()).Returns([redemption]);
        _codeProtector.Protect("GIFT-1234").Returns("ct:gift");
        _unitOfWork
            .SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("database down"));
        StubAccepted(fulfillmentCode: "GIFT-1234");

        var act = () => _sut.ExecuteAsync();

        await act.Should().ThrowAsync<InvalidOperationException>();
        _codeSignal.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void An_accepted_answer_never_prints_its_code()
    {
        var accepted = new ProviderRedemptionResult.Accepted(null, true, "GIFT-1234");

        accepted.ToString().Should().NotContain("GIFT-1234").And.Contain("<redacted>");
    }

    private void StubAccepted(string? fulfillmentCode) =>
        _seApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProviderRedemptionResult.Accepted(null, Completed: true, fulfillmentCode));

    private void SetupUser()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "user@test.com" };
        _credentials.With(
            new ProviderCredential(
                user.Id,
                StoreProvider.StreamElements,
                "encrypted-token",
                DateTimeOffset.UtcNow
            )
        );
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);
        _tokenEncryption.Unprotect("encrypted-token").Returns("decrypted-jwt");
    }

    private Redemption CreatePendingRedemption(
        string channelId,
        string seItemId,
        string? message = null,
        StoreProvider provider = StoreProvider.StreamElements
    )
    {
        var storeId = Guid.NewGuid();
        var item = new StoreItem(
            Guid.NewGuid(),
            storeId,
            new StoreItemApiData(
                provider,
                new ProviderItemId(seItemId),
                new StoreExternalId(channelId),
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
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow
            )
        );
        item.ClearDomainEvents();
        var watchedStore = new WatchedStore(
            storeId,
            provider,
            new StoreExternalId(channelId),
            "Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
        typeof(StoreItem).GetProperty(nameof(StoreItem.WatchedStore))!.SetValue(item, watchedStore);
        _storeItemRepo.GetByIdWithStoreAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);

        var entry = new WishlistEntry(
            Guid.NewGuid(),
            Guid.NewGuid(),
            item.Id,
            DateTimeOffset.UtcNow
        );
        return entry.CreateRedemption(item, message, "Item", "Store", DateTimeOffset.UtcNow);
    }

    private static ProviderRedemptionResult CreateSeRedemption(string id, bool completed) =>
        new ProviderRedemptionResult.Accepted(id, completed, FulfillmentCode: null);
}
