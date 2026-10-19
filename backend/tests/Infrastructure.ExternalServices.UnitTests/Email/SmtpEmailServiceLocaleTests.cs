using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using PointLurker.Core.Application.Notifications;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace Infrastructure.ExternalServices.UnitTests.Email;

/// <summary>
/// Tests that SmtpEmailService renders emails in the correct locale
/// using real .resx resources (not mocked IStringLocalizer).
/// </summary>
public sealed class SmtpEmailServiceLocaleTests
{
    private static (SmtpEmailService Service, Func<MimeMessage?> Sent) BuildService()
    {
        var transport = Substitute.For<ISmtpTransport>();
        MimeMessage? sent = null;
        transport
            .When(t => t.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()))
            .Do(ci => sent = ci.Arg<MimeMessage>());

        var settings = Options.Create(
            new SmtpSettings
            {
                Host = "smtp.example.com",
                Username = "user",
                Password = "pass",
                From = "noreply@example.com",
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
        return (service, () => sent);
    }

    private static AvailabilityEmailModel OneItemModel() =>
        AvailabilityEmailFixtures.OneItemModel();

    [Fact]
    public async Task SendAvailability_with_enUS_locale_uses_English_subject()
    {
        var (service, sent) = BuildService();

        await service.SendAvailabilityAsync("user@test.com", OneItemModel(), locale: "en-US");

        sent()!.Subject.Should().Contain("became available");
    }

    [Fact]
    public async Task SendAvailability_with_ptBR_locale_uses_Portuguese_subject()
    {
        var (service, sent) = BuildService();

        await service.SendAvailabilityAsync("user@test.com", OneItemModel(), locale: "pt-BR");

        sent()!.Subject.Should().Contain("ficou disponível");
    }

    [Fact]
    public async Task SendAvailability_with_ptBR_locale_uses_Portuguese_body()
    {
        var (service, sent) = BuildService();

        await service.SendAvailabilityAsync("user@test.com", OneItemModel(), locale: "pt-BR");

        sent()!.HtmlBody.Should().Contain("Itens ficaram");
    }

    [Fact]
    public async Task SendAvailability_with_unknown_locale_falls_back_to_English()
    {
        var (service, sent) = BuildService();

        await service.SendAvailabilityAsync("user@test.com", OneItemModel(), locale: "xx-XX");

        sent()!.Subject.Should().Contain("became available");
    }

    [Fact]
    public async Task Ambient_culture_is_restored_after_send()
    {
        var (service, _) = BuildService();
        var before = CultureInfo.CurrentUICulture;

        await service.SendAvailabilityAsync("user@test.com", OneItemModel(), locale: "pt-BR");

        CultureInfo.CurrentUICulture.Should().Be(before);
    }
}
