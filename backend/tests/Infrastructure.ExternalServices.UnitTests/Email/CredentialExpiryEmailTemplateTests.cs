using FluentAssertions;
using MimeKit;
using NSubstitute;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Infrastructure.ExternalServices.Email;
using static Infrastructure.ExternalServices.UnitTests.Email.AvailabilityEmailFixtures;

namespace Infrastructure.ExternalServices.UnitTests.Email;

public sealed class CredentialExpiryEmailTemplateTests
{
    private static readonly CredentialExpiryEmailModel Expiring = new(
        StoreProvider.GauPoints,
        new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero),
        HasExpired: false
    );

    private static readonly CredentialExpiryEmailModel Expired = Expiring with
    {
        HasExpired = true,
    };

    [Fact]
    public void The_subject_names_the_provider_and_whether_it_has_expired()
    {
        var template = ExpiryTemplate();

        InCulture("en-US", () => template.RenderSubject(Expiring))
            .Should()
            .Be("Your GauPoints sign-in expires soon");
        InCulture("en-US", () => template.RenderSubject(Expired))
            .Should()
            .Be("Your GauPoints sign-in has expired");
    }

    [Fact]
    public void The_body_says_when_it_stops_working_in_utc()
    {
        var html = InCulture("en-US", () => ExpiryTemplate().RenderHtml(Expiring));

        // The space before the meridiem is whatever the ICU build uses — a narrow no-break space on
        // current ones — so the assertion stops either side of it.
        html.Should()
            .Contain(
                "The GauPoints credential stored in PointLurker stops working on Monday, October 5, 2026 2:30"
            )
            .And.Contain("PM UTC.");
        html.Should().Contain("paste it on the Settings page");
    }

    [Fact]
    public void The_instant_is_written_in_utc_whatever_offset_it_carries()
    {
        var html = InCulture(
            "en-US",
            () =>
                ExpiryTemplate()
                    .RenderHtml(
                        Expiring with
                        {
                            ExpiresAt = new DateTimeOffset(
                                2026,
                                10,
                                5,
                                11,
                                30,
                                0,
                                TimeSpan.FromHours(-3)
                            ),
                        }
                    )
        );

        html.Should().Contain("October 5, 2026 2:30").And.NotContain("11:30");
    }

    [Fact]
    public void An_expired_credential_is_described_in_the_past_tense()
    {
        var html = InCulture("en-US", () => ExpiryTemplate().RenderHtml(Expired));

        html.Should().Contain("Your GauPoints sign-in has expired");
        html.Should().Contain("stopped working on");
    }

    [Fact]
    public void Portuguese_text_keeps_its_accents_and_its_date_format()
    {
        var html = InCulture("pt-BR", () => ExpiryTemplate().RenderHtml(Expiring));

        html.Should().Contain("Seu login do GauPoints expira em breve");
        html.Should().Contain("resgate automático");
        html.Should().Contain("5 de outubro de 2026");
    }

    [Fact]
    public void The_settings_link_appears_only_with_a_published_url()
    {
        var withUrl = InCulture(
            "en-US",
            () => ExpiryTemplate("https://monitor.example").RenderHtml(Expiring)
        );
        var withoutUrl = InCulture("en-US", () => ExpiryTemplate().RenderHtml(Expiring));

        withUrl.Should().Contain("href=\"https://monitor.example/settings\"");
        withoutUrl.Should().NotContain("href=");
    }

    [Fact]
    public async Task The_send_path_mails_the_rendered_warning()
    {
        var transport = Substitute.For<ISmtpTransport>();
        MimeMessage? sent = null;
        transport
            .When(t => t.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()))
            .Do(ci => sent = ci.Arg<MimeMessage>());
        var service = new SmtpEmailService(
            transport,
            Microsoft.Extensions.Options.Options.Create(
                new SmtpSettings
                {
                    Host = "smtp.example.com",
                    Username = "user",
                    Password = "pass",
                    From = "noreply@example.com",
                    RetryBaseDelaySeconds = 0,
                }
            ),
            Template(),
            FulfillmentTemplate(),
            ExpiryTemplate()
        );

        await service.SendCredentialExpiryAsync("owner@example.com", Expired, "pt-BR");

        sent!.To.Mailboxes.Single().Address.Should().Be("owner@example.com");
        sent.Subject.Should().Be("Seu login do GauPoints expirou");
        sent.HtmlBody.Should().Contain("deixou de funcionar");
    }
}
