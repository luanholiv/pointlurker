using FluentAssertions;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using PointLurker.Core.Application.Notifications;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace Infrastructure.ExternalServices.UnitTests.Email;

/// <summary>
/// The send path: addressing, the message MIME shape, and the guard on an empty model. What the
/// body looks like belongs to <see cref="AvailabilityEmailTemplateTests"/>.
/// </summary>
public sealed class SmtpEmailServiceTests
{
    private readonly ISmtpTransport _transport = Substitute.For<ISmtpTransport>();
    private readonly SmtpEmailService _service;
    private const string FromAddress = "noreply@example.com";

    private MimeMessage? _sent;

    public SmtpEmailServiceTests()
    {
        _service = new SmtpEmailService(
            _transport,
            Settings(FromAddress),
            AvailabilityEmailFixtures.Template(),
            AvailabilityEmailFixtures.FulfillmentTemplate(),
            AvailabilityEmailFixtures.ExpiryTemplate()
        );

        _transport
            .When(t => t.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>()))
            .Do(ci => _sent = ci.Arg<MimeMessage>());
    }

    private static IOptions<SmtpSettings> Settings(string from) =>
        Options.Create(
            new SmtpSettings
            {
                Host = "smtp.example.com",
                Username = "user",
                Password = "pass",
                From = from,
                RetryBaseDelaySeconds = 0,
            }
        );

    [Fact]
    public async Task Sends_message_with_correct_from_to_and_subject()
    {
        await _service.SendAvailabilityAsync(
            "user@test.com",
            AvailabilityEmailFixtures.OneItemModel()
        );

        await _transport
            .Received(1)
            .SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
        _sent.Should().NotBeNull();
        _sent!.From.Mailboxes.Single().Address.Should().Be(FromAddress);
        _sent.To.Mailboxes.Single().Address.Should().Be("user@test.com");
        _sent.Subject.Should().Be("PointLurker: 1 item became available");
    }

    [Fact]
    public async Task From_with_display_name_is_parsed()
    {
        var service = new SmtpEmailService(
            _transport,
            Settings("Monitor <noreply@example.com>"),
            AvailabilityEmailFixtures.Template(),
            AvailabilityEmailFixtures.FulfillmentTemplate(),
            AvailabilityEmailFixtures.ExpiryTemplate()
        );

        await service.SendAvailabilityAsync(
            "user@test.com",
            AvailabilityEmailFixtures.OneItemModel()
        );

        _sent!.From.Mailboxes.Single().Name.Should().Be("Monitor");
        _sent.From.Mailboxes.Single().Address.Should().Be("noreply@example.com");
    }

    [Fact]
    public async Task Subject_counts_items_across_every_store()
    {
        var model = AvailabilityEmailFixtures.Model(
            AvailabilityEmailFixtures.Store(
                "Store A",
                AvailabilityEmailFixtures.Item("Item 1"),
                AvailabilityEmailFixtures.Item("Item 2")
            ),
            AvailabilityEmailFixtures.Store("Store B", AvailabilityEmailFixtures.Item("Item 3"))
        );

        await _service.SendAvailabilityAsync("user@test.com", model);

        _sent!.Subject.Should().Be("PointLurker: 3 items became available");
    }

    [Fact]
    public async Task Body_is_the_rendered_template()
    {
        var model = AvailabilityEmailFixtures.OneItemModel("Neon Skin Pack");

        await _service.SendAvailabilityAsync("user@test.com", model);

        _sent!
            .HtmlBody.Should()
            .Be(
                AvailabilityEmailFixtures.InCulture(
                    "en-US",
                    () => AvailabilityEmailFixtures.Template().RenderHtml(model)
                )
            );
    }

    [Fact]
    public async Task Throws_ArgumentException_for_empty_model_without_sending()
    {
        var model = new AvailabilityEmailModel([]);

        var act = () => _service.SendAvailabilityAsync("user@test.com", model);

        await act.Should().ThrowAsync<ArgumentException>();
        await _transport
            .DidNotReceive()
            .SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>());
    }
}
