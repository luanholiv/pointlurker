using System.ComponentModel.DataAnnotations;
using System.Net.Sockets;
using FluentAssertions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using PointLurker.Core.Application.Notifications;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace Infrastructure.ExternalServices.UnitTests.Email;

public sealed class SmtpEmailServiceRetryTests
{
    private static (SmtpEmailService Service, Func<int> Attempts) BuildService(
        Func<int, Task> behavior,
        int maxRetries = 3
    )
    {
        var transport = Substitute.For<ISmtpTransport>();
        var attempts = 0;
        transport
            .SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                attempts++;
                return behavior(attempts);
            });

        var settings = Options.Create(
            new SmtpSettings
            {
                Host = "smtp.example.com",
                Username = "user",
                Password = "pass",
                From = "noreply@example.com",
                MaxRetries = maxRetries,
                RetryBaseDelaySeconds = 0,
            }
        );
        var service = new SmtpEmailService(
            transport,
            settings,
            AvailabilityEmailFixtures.Template(),
            AvailabilityEmailFixtures.FulfillmentTemplate(),
            AvailabilityEmailFixtures.ExpiryTemplate()
        );
        return (service, () => attempts);
    }

    private static AvailabilityEmailModel Model() => AvailabilityEmailFixtures.OneItemModel();

    private static SmtpCommandException Transient4xx() =>
        new(
            SmtpErrorCode.UnexpectedStatusCode,
            SmtpStatusCode.ServiceNotAvailable,
            "421 try later"
        );

    private static SmtpCommandException Permanent5xx() =>
        new(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.MailboxUnavailable, "550 no mailbox");

    [Fact]
    public async Task Transient_socket_error_then_success_sends_after_retry()
    {
        var (service, attempts) = BuildService(attempt =>
            attempt == 1 ? Task.FromException(new SocketException()) : Task.CompletedTask
        );

        await service.SendAvailabilityAsync("user@test.com", Model());

        attempts().Should().Be(2);
    }

    [Fact]
    public async Task Transient_smtp_4xx_then_success_retries()
    {
        var (service, attempts) = BuildService(attempt =>
            attempt == 1 ? Task.FromException(Transient4xx()) : Task.CompletedTask
        );

        await service.SendAvailabilityAsync("user@test.com", Model());

        attempts().Should().Be(2);
    }

    [Fact]
    public async Task Permanent_smtp_5xx_is_not_retried()
    {
        var (service, attempts) = BuildService(_ => Task.FromException(Permanent5xx()));

        var act = () => service.SendAvailabilityAsync("user@test.com", Model());

        await act.Should().ThrowAsync<SmtpCommandException>();
        attempts().Should().Be(1);
    }

    [Fact]
    public async Task Authentication_failure_is_not_retried()
    {
        var (service, attempts) = BuildService(_ =>
            Task.FromException(new AuthenticationException("auth failed"))
        );

        var act = () => service.SendAvailabilityAsync("user@test.com", Model());

        await act.Should().ThrowAsync<AuthenticationException>();
        attempts().Should().Be(1);
    }

    [Fact]
    public async Task Cancellation_is_not_retried()
    {
        var (service, attempts) = BuildService(_ =>
            Task.FromException(new OperationCanceledException())
        );

        var act = () => service.SendAvailabilityAsync("user@test.com", Model());

        await act.Should().ThrowAsync<OperationCanceledException>();
        attempts().Should().Be(1);
    }

    [Fact]
    public async Task Retries_exhausted_propagates_final_exception()
    {
        var (service, attempts) = BuildService(
            _ => Task.FromException(new SocketException()),
            maxRetries: 2
        );

        var act = () => service.SendAvailabilityAsync("user@test.com", Model());

        await act.Should().ThrowAsync<SocketException>();
        // 1 initial attempt + 2 retries.
        attempts().Should().Be(3);
    }

    /// <summary>
    /// Records why <see cref="SmtpSettings.MaxRetries"/> has a floor of 1 rather than 0.
    /// <para>
    /// The retry strategy rejects a zero attempt count, and it does so while the pipeline is being
    /// built — in this service's constructor. A settings object that permitted 0 would therefore
    /// bind and validate cleanly and then take the process down on the first resolution, pointing
    /// at a pipeline rather than at the setting that caused it. The floor keeps the complaint where
    /// an operator can act on it.
    /// </para>
    /// </summary>
    [Fact]
    public void Constructing_with_zero_retries_is_rejected_by_the_retry_strategy()
    {
        var act = () => BuildService(_ => Task.CompletedTask, maxRetries: 0);

        act.Should()
            .Throw<ValidationException>()
            .WithMessage("*MaxRetryAttempts must be between 1*");
    }
}
