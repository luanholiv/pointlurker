using System.Globalization;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Localization;
using Polly;
using Polly.Retry;

namespace PointLurker.Infrastructure.ExternalServices.Email;

public sealed class SmtpEmailService(
    ISmtpTransport transport,
    IOptions<SmtpSettings> settings,
    AvailabilityEmailTemplate template,
    FulfillmentCodeEmailTemplate fulfillmentTemplate,
    CredentialExpiryEmailTemplate expiryTemplate
) : IEmailNotifier
{
    private readonly SmtpSettings _settings = settings.Value;
    private readonly ResiliencePipeline _pipeline = BuildPipeline(settings.Value);

    public Task SendAvailabilityAsync(
        string toAddress,
        AvailabilityEmailModel model,
        string locale = SupportedLocales.EnUs,
        CancellationToken cancellationToken = default
    )
    {
        if (model.Stores.Count == 0)
        {
            throw new ArgumentException(
                "Email model must contain at least one store with items.",
                nameof(model)
            );
        }

        return SendAsync(
            toAddress,
            locale,
            () => (template.RenderSubject(model), template.RenderHtml(model)),
            cancellationToken
        );
    }

    public Task SendFulfillmentCodeAsync(
        string toAddress,
        FulfillmentCodeEmailModel model,
        string locale = SupportedLocales.EnUs,
        CancellationToken cancellationToken = default
    ) =>
        SendAsync(
            toAddress,
            locale,
            () => (fulfillmentTemplate.RenderSubject(model), fulfillmentTemplate.RenderHtml(model)),
            cancellationToken
        );

    public Task SendCredentialExpiryAsync(
        string toAddress,
        CredentialExpiryEmailModel model,
        string locale = SupportedLocales.EnUs,
        CancellationToken cancellationToken = default
    ) =>
        SendAsync(
            toAddress,
            locale,
            () => (expiryTemplate.RenderSubject(model), expiryTemplate.RenderHtml(model)),
            cancellationToken
        );

    /// <param name="render">Called under the recipient's culture, which the templates read.</param>
    private async Task SendAsync(
        string toAddress,
        string locale,
        Func<(string Subject, string Html)> render,
        CancellationToken cancellationToken
    )
    {
        // Fall back to en-US for any locale not on the supported allowlist.
        var effectiveCulture = SupportedLocales.IsSupported(locale)
            ? new CultureInfo(locale)
            : new CultureInfo(SupportedLocales.EnUs);

        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousCulture = CultureInfo.CurrentCulture;

        CultureInfo.CurrentUICulture = effectiveCulture;
        CultureInfo.CurrentCulture = effectiveCulture;

        try
        {
            var (subject, html) = render();
            var message = BuildMessage(toAddress, subject, html);

            await _pipeline.ExecuteAsync(
                async token => await transport.SendAsync(message, token),
                cancellationToken
            );
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private MimeMessage BuildMessage(string toAddress, string subject, string html)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.From));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        return message;
    }

    private static ResiliencePipeline BuildPipeline(SmtpSettings settings)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder()
                        .Handle<SocketException>()
                        .Handle<IOException>()
                        .Handle<TimeoutException>()
                        .Handle<SmtpProtocolException>()
                        .Handle<SmtpCommandException>(IsTransient),
                    MaxRetryAttempts = settings.MaxRetries,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromSeconds(settings.RetryBaseDelaySeconds),
                }
            )
            .Build();
    }

    // Transient = SMTP 4xx (e.g. 421 service not available, 450/451/452 mailbox busy /
    // local error / insufficient storage). 5xx (e.g. 550 no-such-mailbox) is permanent.
    // AuthenticationException is not an SmtpCommandException, so it is never retried.
    private static bool IsTransient(SmtpCommandException ex) =>
        (int)ex.StatusCode is >= 400 and < 500;
}
