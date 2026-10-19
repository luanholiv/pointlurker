using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace Core.Application.UnitTests.Notifications;

public sealed class DeliverFulfillmentCodesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IFulfillmentCodeRepository _codes =
        Substitute.For<IFulfillmentCodeRepository>();
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();
    private readonly IEmailNotifier _email = Substitute.For<IEmailNotifier>();
    private readonly INotificationRepository _notifications =
        Substitute.For<INotificationRepository>();
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly ApplicationUser _user = new()
    {
        Id = Guid.NewGuid(),
        Email = "owner@example.com",
    };
    private readonly DeliverFulfillmentCodes _sut;

    public DeliverFulfillmentCodesTests()
    {
        _user.SetLocale("pt-BR");
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(_user);
        _protector.Unprotect("ct:gift").Returns("GIFT-1234");

        _sut = new DeliverFulfillmentCodes(
            _codes,
            _protector,
            _email,
            _notifications,
            _userProvider,
            _unitOfWork,
            _time,
            NullLogger<DeliverFulfillmentCodes>.Instance
        );
    }

    [Fact]
    public async Task A_due_code_is_emailed_in_the_owner_locale()
    {
        var delivery = Due();

        await _sut.ExecuteAsync();

        await _email
            .Received(1)
            .SendFulfillmentCodeAsync(
                "owner@example.com",
                new FulfillmentCodeEmailModel("GauPoints", "Steam R$ 50", "GIFT-1234"),
                "pt-BR",
                Arg.Any<CancellationToken>()
            );
        delivery.Code.SentAt.Should().Be(Now);
    }

    [Fact]
    public async Task A_delivered_code_leaves_a_sent_email_notification()
    {
        var delivery = Due();

        await _sut.ExecuteAsync();

        _notifications
            .Received(1)
            .Add(
                Arg.Is<Notification>(n =>
                    n.UserId == _user.Id
                    && n.Channel == NotificationChannel.Email
                    && n.Status == NotificationStatus.Sent
                    && n.Items.Single().StoreItemId == delivery.StoreItemId
                    && n.Items.Single().ItemName == "Steam R$ 50"
                )
            );
        _codes.Received(1).Update(delivery.Code);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_code_commits_on_its_own()
    {
        Due();
        Due();

        await _sut.ExecuteAsync();

        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failed_send_schedules_a_retry_and_writes_no_notification()
    {
        var delivery = Due();
        _email
            .SendFulfillmentCodeAsync(
                Arg.Any<string>(),
                Arg.Any<FulfillmentCodeEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new IOException("connection reset"));

        await _sut.ExecuteAsync();

        delivery.Code.SentAt.Should().BeNull();
        delivery.Code.Attempts.Should().Be(1);
        delivery.Code.NextRetryAt.Should().Be(Now.AddMinutes(1));
        delivery.Code.LastError.Should().Contain("connection reset");
        _notifications.DidNotReceive().Add(Arg.Any<Notification>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(5, 32)]
    [InlineData(20, 360)]
    public async Task The_retry_backs_off_to_a_six_hour_ceiling(int priorFailures, int minutes)
    {
        var delivery = Due();
        for (var i = 0; i < priorFailures; i++)
            delivery.Code.RecordFailedAttempt("earlier", Now);
        _email
            .SendFulfillmentCodeAsync(
                Arg.Any<string>(),
                Arg.Any<FulfillmentCodeEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new IOException("connection reset"));

        await _sut.ExecuteAsync();

        delivery.Code.NextRetryAt.Should().Be(Now.AddMinutes(minutes));
    }

    [Fact]
    public async Task The_code_never_reaches_the_failure_record()
    {
        var delivery = Due();
        _email
            .SendFulfillmentCodeAsync(
                Arg.Any<string>(),
                Arg.Any<FulfillmentCodeEmailModel>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(new InvalidOperationException("rejected body containing GIFT-1234"));

        await _sut.ExecuteAsync();

        delivery.Code.LastError.Should().NotContain("GIFT-1234");
    }

    [Fact]
    public async Task A_code_that_will_not_decrypt_is_retried_rather_than_abandoned()
    {
        var delivery = Due();
        _protector
            .Unprotect("ct:gift")
            .Throws(new System.Security.Cryptography.CryptographicException("key not found"));

        await _sut.ExecuteAsync();

        delivery.Code.SentAt.Should().BeNull();
        delivery.Code.NextRetryAt.Should().NotBeNull();
        delivery.Code.LastError.Should().Contain("decrypt");
        await _email
            .DidNotReceiveWithAnyArgs()
            .SendFulfillmentCodeAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task An_account_without_an_address_is_retried_rather_than_abandoned()
    {
        _user.Email = null;
        _user.UserName = null;
        var delivery = Due();

        await _sut.ExecuteAsync();

        delivery.Code.SentAt.Should().BeNull();
        delivery.Code.NextRetryAt.Should().NotBeNull();
        await _email
            .DidNotReceiveWithAnyArgs()
            .SendFulfillmentCodeAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Nothing_is_read_before_the_owner_exists()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        await _sut.ExecuteAsync();

        await _codes.DidNotReceiveWithAnyArgs().GetDueForUpdateAsync(default, default);
    }

    private readonly List<PendingFulfillmentDelivery> _due = [];

    private PendingFulfillmentDelivery Due()
    {
        var delivery = new PendingFulfillmentDelivery(
            new FulfillmentCode(Guid.NewGuid(), Guid.NewGuid(), "ct:gift", Now.AddMinutes(-1)),
            Guid.NewGuid(),
            "Steam R$ 50",
            "GauPoints"
        );
        _due.Add(delivery);
        _codes.GetDueForUpdateAsync(Now, Arg.Any<CancellationToken>()).Returns(_due.ToArray());
        return delivery;
    }
}
