using FluentAssertions;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Notifications;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace IntegrationTests.Email;

[Collection(ContainersCollection.Name)]
public sealed class SmtpEmailServiceIntegrationTests(MailpitFixture mailpit) : IAsyncLifetime
{
    // The container is shared with every other test in the collection, so "the captured message"
    // only names one message if this test put it there.
    public Task InitializeAsync() => mailpit.Messages.ClearAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static IStringLocalizer<EmailResources> RealLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        return services
            .BuildServiceProvider()
            .GetRequiredService<IStringLocalizer<EmailResources>>();
    }

    private SmtpEmailService BuildService(
        string host,
        int port,
        int maxRetries = 3,
        string from = "Monitor <monitor@example.com>"
    )
    {
        var settings = Options.Create(
            new SmtpSettings
            {
                Host = host,
                Port = port,
                Username = "test",
                Password = "test",
                From = from,
                Security = SecureSocketOptions.None,
                MaxRetries = maxRetries,
                RetryBaseDelaySeconds = 0,
            }
        );
        var transport = new MailKitSmtpTransport(settings);
        return new SmtpEmailService(
            transport,
            settings,
            new AvailabilityEmailTemplate(RealLocalizer(), Options.Create(new AppUrlSettings())),
            new FulfillmentCodeEmailTemplate(RealLocalizer(), Options.Create(new AppUrlSettings())),
            new CredentialExpiryEmailTemplate(RealLocalizer(), Options.Create(new AppUrlSettings()))
        );
    }

    private static AvailabilityEmailItem Item(string name, long cost, bool pending = false) =>
        new(name, cost, pending, "SKIN", false, -1, -1);

    private static AvailabilityEmailModel TwoStoreModel() =>
        new([
            new AvailabilityEmailStoreGroup(
                "Store A",
                Guid.NewGuid(),
                [Item("Item 1", 100L), Item("Item 2", 200L, pending: true)]
            ),
            new AvailabilityEmailStoreGroup("Store B", Guid.NewGuid(), [Item("Item 3", 300L)]),
        ]);

    [Fact]
    public async Task Sends_real_email_captured_by_mailpit()
    {
        var service = BuildService(mailpit.SmtpHost, mailpit.MappedSmtpPort);

        await service.SendAvailabilityAsync("recipient@test.com", TwoStoreModel(), locale: "en-US");

        var message = await mailpit.Messages.WaitForSingleMessageAsync();

        message.From.Address.Should().Be("monitor@example.com");
        message.To[0].Address.Should().Be("recipient@test.com");
        message.Subject.Should().Contain("became available");

        var html = message.Html ?? string.Empty;
        html.Should().Contain("Store A").And.Contain("Store B");
        html.Should().Contain("Item 1").And.Contain("Item 2").And.Contain("Item 3");

        // Delivery is what this test is for, so it checks the one thing a real SMTP round trip
        // can break that a render test cannot: that the non-ASCII glyphs and pills survive
        // transfer encoding intact.
        html.Should().Contain("AUTO-REDEEM PENDING").And.Contain("&#9670;");
    }

    [Fact]
    public async Task Unreachable_host_throws_after_retries()
    {
        // Port 1 is reserved/closed — connection is refused. With MaxRetries small and
        // zero backoff, the send exhausts retries quickly and propagates the failure.
        var service = BuildService(mailpit.SmtpHost, port: 1, maxRetries: 1);

        var act = () =>
            service.SendAvailabilityAsync("recipient@test.com", TwoStoreModel(), locale: "en-US");

        await act.Should().ThrowAsync<Exception>();
        (await mailpit.Messages.CountAsync()).Should().Be(0);
    }
}
