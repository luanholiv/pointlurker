using FluentAssertions;
using MimeKit;
using NSubstitute;
using PointLurker.Core.Application.Notifications;
using PointLurker.Infrastructure.ExternalServices.Email;
using static Infrastructure.ExternalServices.UnitTests.Email.AvailabilityEmailFixtures;

namespace Infrastructure.ExternalServices.UnitTests.Email;

public sealed class FulfillmentCodeEmailTemplateTests
{
    private static readonly FulfillmentCodeEmailModel Model = new(
        "GauPoints",
        "Steam R$ 50",
        "ABCD-EFGH-1234"
    );

    [Fact]
    public void The_body_carries_the_code_and_what_it_was_for()
    {
        var html = InCulture("en-US", () => FulfillmentTemplate().RenderHtml(Model));

        html.Should().Contain("ABCD-EFGH-1234");
        html.Should().Contain("Steam R$ 50 was redeemed from GauPoints.");
    }

    /// <summary>
    /// The subject and preheader are what a lock screen shows, so the code stays out of both.
    /// </summary>
    [Fact]
    public void The_code_stays_out_of_the_subject_and_the_preview()
    {
        var template = FulfillmentTemplate();

        var subject = InCulture("en-US", () => template.RenderSubject(Model));
        var html = InCulture("en-US", () => template.RenderHtml(Model));
        var preheader = html[html.IndexOf("display:none", StringComparison.Ordinal)..];
        preheader = preheader[..preheader.IndexOf("</div>", StringComparison.Ordinal)];

        subject.Should().Be("Your gift-card code for Steam R$ 50");
        subject.Should().NotContain(Model.Code);
        preheader.Should().NotContain(Model.Code);
    }

    [Fact]
    public void Markup_in_the_code_is_escaped()
    {
        var html = InCulture(
            "en-US",
            () => FulfillmentTemplate().RenderHtml(Model with { Code = "<b>&x" })
        );

        html.Should().Contain("&lt;b&gt;&amp;x").And.NotContain("<b>&x");
    }

    [Fact]
    public void Portuguese_text_keeps_its_accents()
    {
        var html = InCulture("pt-BR", () => FulfillmentTemplate().RenderHtml(Model));

        html.Should().Contain("Seu código chegou").And.Contain("CÓDIGO");
    }

    [Fact]
    public void The_history_link_appears_only_with_a_published_url()
    {
        var withUrl = InCulture(
            "en-US",
            () => FulfillmentTemplate("https://monitor.example").RenderHtml(Model)
        );
        var withoutUrl = InCulture("en-US", () => FulfillmentTemplate().RenderHtml(Model));

        withUrl.Should().Contain("href=\"https://monitor.example/redemptions\"");
        withoutUrl.Should().NotContain("href=");
    }

    [Fact]
    public async Task The_send_path_mails_the_rendered_code()
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

        await service.SendFulfillmentCodeAsync("owner@example.com", Model, "pt-BR");

        sent!.To.Mailboxes.Single().Address.Should().Be("owner@example.com");
        sent.Subject.Should().Be("Seu código de gift card de Steam R$ 50");
        sent.HtmlBody.Should().Contain("ABCD-EFGH-1234");
    }
}
