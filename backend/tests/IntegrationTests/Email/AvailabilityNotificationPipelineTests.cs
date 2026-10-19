using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Email;

/// <summary>
/// Drives a real sync cycle all the way to a message sitting in a real SMTP server.
/// <para>
/// The pipeline is covered on both sides of this point and nowhere across it: the sync tests
/// substitute the notifier, so no message is ever built, and the SMTP test hand-builds its model,
/// so no user, store or wishlist entry is involved. Recipient, locale, subject and body are
/// therefore never checked together for an account that actually exists. That is what this does.
/// </para>
/// <para>
/// Everything is real except the StreamElements catalogue: real Postgres, real repositories, real
/// <c>SyncStores</c>, real <c>SmtpEmailService</c> over MailKit, real resources. The web factory
/// needs no new hook to allow it — leaving its notifier substitute unset is what lets the genuine
/// registration stand, and the SMTP settings are pointed at the container through configuration.
/// </para>
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class AvailabilityNotificationPipelineTests(
    DatabaseFixture database,
    MailpitFixture mailpit
) : IAsyncLifetime, IDisposable
{
    private const string UserEmail = "sync@test.com";
    private const string UserPassword = "Password123!";
    private const string ChannelId = "ch-1";
    private const string FromAddress = "monitor@example.com";
    private const string FromDisplayName = "PointLurker";

    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await mailpit.Messages.ClearAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// Starts the app with its mail settings pointed at the Mailpit container.
    /// <para>
    /// <c>Smtp:Security</c> has to be stated: it defaults to STARTTLS, which the container's
    /// plaintext listener will not complete. Retries are held to the minimum the retry strategy
    /// accepts, with no backoff, so the failure cases finish as fast as they fail.
    /// </para>
    /// </summary>
    /// <param name="smtpPort">
    /// Defaults to the container. Pass a closed port to exercise a send that never arrives.
    /// </param>
    private void StartHost(int? smtpPort = null)
    {
        _factory = new TestWebApplicationFactory(
            database.ConnectionString,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Smtp:Host"] = mailpit.SmtpHost,
                ["Smtp:Port"] = (smtpPort ?? mailpit.MappedSmtpPort).ToString(),
                ["Smtp:Security"] = "None",
                ["Smtp:Username"] = "test",
                ["Smtp:Password"] = "test",
                ["Smtp:From"] = $"{FromDisplayName} <{FromAddress}>",
                ["Smtp:MaxRetries"] = "1",
                ["Smtp:RetryBaseDelaySeconds"] = "0",
            }
        )
        {
            MockStreamElementsApi = _seApi,
        };
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Sync_delivers_a_real_email_for_a_wishlisted_item_that_became_available()
    {
        StartHost();
        await ArrangeWishlistedItemBecomingAvailableAsync("Store One", "Notify Item");

        var result = await RunSyncAsync();

        result.NotificationsSent.Should().Be(1);

        var message = await mailpit.Messages.WaitForSingleMessageAsync();
        message.To.Should().ContainSingle().Which.Address.Should().Be(UserEmail);
        message.From.Address.Should().Be(FromAddress);
        message.From.Name.Should().Be(FromDisplayName);
        message.Subject.Should().Contain("became available");

        var html = message.Html ?? string.Empty;
        html.Should().Contain("Store One").And.Contain("Notify Item");

        var notification = await SingleEmailNotificationAsync();
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.SentAt.Should().NotBeNull();
        notification.FailureReason.Should().BeNull();
        var item = notification.Items.Should().ContainSingle().Subject;
        item.StoreName.Should().Be("Store One");
        item.ItemName.Should().Be("Notify Item");
    }

    /// <summary>
    /// The account's locale has to reach the wire. Every existing test of this path accepts any
    /// locale argument, so nothing yet proves the stored value is the one that selects the message.
    /// <para>
    /// Only the translated text can show it. A cost small enough to need no digit grouping renders
    /// identically under either culture, so the number cannot stand in for the assertion.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Users_locale_selects_the_delivered_subject_and_body()
    {
        StartHost();
        var accessToken = await ArrangeWishlistedItemBecomingAvailableAsync(
            "Store One",
            "Notify Item"
        );
        var localeResponse = await AuthenticatedPatchAsync(
            accessToken,
            "/api/users/me/locale",
            new { locale = "pt-BR" }
        );
        localeResponse.IsSuccessStatusCode.Should().BeTrue();

        await RunSyncAsync();

        var message = await mailpit.Messages.WaitForSingleMessageAsync();
        message.Subject.Should().Contain("ficou disponível");
        (message.Html ?? string.Empty).Should().Contain("Itens ficaram disponíveis!");
        (await SingleEmailNotificationAsync()).Status.Should().Be(NotificationStatus.Sent);
    }

    /// <summary>
    /// Identity keeps the address and the sign-in name in separate columns and only the second is
    /// mandatory, so an account can reach the notifier with no email address and still be usable.
    /// The user name is the fallback, and here it is a deliverable address.
    /// </summary>
    [Fact]
    public async Task Recipient_falls_back_to_the_user_name_when_the_email_column_is_null()
    {
        StartHost();
        await ArrangeWishlistedItemBecomingAvailableAsync("Store One", "Notify Item");
        // After every authenticated call: clearing the address invalidates nothing, but there is no
        // reason to make the arrange depend on that. The sync itself runs off a DI scope, not HTTP.
        await ClearIdentityColumnsAsync(clearUserName: false);

        var result = await RunSyncAsync();

        result.NotificationsSent.Should().Be(1);
        var message = await mailpit.Messages.WaitForSingleMessageAsync();
        message.To.Should().ContainSingle().Which.Address.Should().Be(UserEmail);
        (await SingleEmailNotificationAsync()).Status.Should().Be(NotificationStatus.Sent);
    }

    /// <summary>
    /// With neither column there is nowhere to send, and the send is abandoned rather than
    /// attempted against a placeholder. The items are still recorded: what became available is
    /// worth keeping whether or not the account could be told about it.
    /// </summary>
    [Fact]
    public async Task An_unaddressable_user_records_a_failed_notification_and_delivers_nothing()
    {
        StartHost();
        await ArrangeWishlistedItemBecomingAvailableAsync("Store One", "Notify Item");
        await ClearIdentityColumnsAsync(clearUserName: true);

        var result = await RunSyncAsync();

        result.NotificationsSent.Should().Be(0);
        (await mailpit.Messages.CountAsync()).Should().Be(0);

        var notification = await SingleEmailNotificationAsync();
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.FailureReason.Should().NotBeNullOrWhiteSpace();
        notification.SentAt.Should().BeNull();
        notification.Items.Should().ContainSingle();
    }

    /// <summary>
    /// A send that never reaches a server still has to leave a record, items included — the items
    /// are attached after the failure is caught, and nothing else proves that ordering survives.
    /// </summary>
    [Fact]
    public async Task A_send_that_never_reaches_a_server_persists_as_failed_with_its_items()
    {
        // Port 1 is reserved and refuses the connection, the same trick the transport test uses.
        StartHost(smtpPort: 1);
        await ArrangeWishlistedItemBecomingAvailableAsync("Store One", "Notify Item");

        var result = await RunSyncAsync();

        result.NotificationsSent.Should().Be(0);
        (await mailpit.Messages.CountAsync()).Should().Be(0);

        var notification = await SingleEmailNotificationAsync();
        notification.Status.Should().Be(NotificationStatus.Failed);
        // Not the text: a socket error's wording follows the host's OS and language.
        notification.FailureReason.Should().NotBeNullOrWhiteSpace();
        notification.SentAt.Should().BeNull();
        notification.Items.Should().ContainSingle();
    }

    /// <summary>
    /// Store and item names reach the body from outside — one typed by the account holder, one
    /// served by StreamElements — so both are escaped on the way in.
    /// <para>
    /// Escaped once. The template encodes every value it interpolates, which is the arrangement
    /// that would silently double-encode if the rendering ever gained a second pass, so the
    /// absence of doubled entities is asserted as well as their presence. The bold tag it rules
    /// out is the one from the item name — the layout itself emits none, which a template test
    /// pins so this assertion keeps meaning what it says.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Store_and_item_names_are_html_escaped_exactly_once_in_the_delivered_body()
    {
        StartHost();
        await ArrangeWishlistedItemBecomingAvailableAsync(
            "Store <A> & \"B\"",
            "Item <b>&\"x\"</b>"
        );

        await RunSyncAsync();

        var html = (await mailpit.Messages.WaitForSingleMessageAsync()).Html ?? string.Empty;
        html.Should().Contain("Store &lt;A&gt; &amp; &quot;B&quot;");
        html.Should().Contain("Item &lt;b&gt;&amp;&quot;x&quot;&lt;/b&gt;");
        html.Should().NotContain("&amp;lt;").And.NotContain("&amp;amp;");
        html.Should().NotContain("<b>").And.NotContain("</b>");

        // Storage keeps what StreamElements said. Escaping belongs to rendering, not to the record.
        var item = (await SingleEmailNotificationAsync()).Items.Single();
        item.StoreName.Should().Be("Store <A> & \"B\"");
        item.ItemName.Should().Be("Item <b>&\"x\"</b>");
    }

    #region Arrange

    /// <summary>
    /// Leaves one wishlisted item that is persisted unavailable and served as available, which is
    /// the transition a single sync cycle then observes.
    /// <para>
    /// Items are imported by the create-store handler rather than by a cycle, so the row is born
    /// unavailable without any sync having run — and one cycle is enough to flip it.
    /// </para>
    /// </summary>
    private async Task<string> ArrangeWishlistedItemBecomingAvailableAsync(
        string storeName,
        string itemName
    )
    {
        var accessToken = await RegisterAndSignInAsync();

        _seApi
            .GetStoreItemsAsync(new StoreExternalId(ChannelId), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem(itemName, enabled: false, quantity: 0)]);

        var createResponse = await AuthenticatedPostAsync(
            accessToken,
            "/api/stores",
            new { channelId = ChannelId, displayName = storeName }
        );
        createResponse.IsSuccessStatusCode.Should().BeTrue();
        var store = await createResponse.Content.ReadFromJsonAsync<StoreDetail>();

        // avail=0 turns off the availability filter, which otherwise hides the sold-out row.
        var itemsResponse = await AuthenticatedGetAsync(
            accessToken,
            $"/api/stores/{store!.Id}/items?avail=0&limit=200"
        );
        var items = await itemsResponse.Content.ReadFromJsonAsync<StoreItemPage>();
        var item = items!.Items.Single();
        item.IsAvailable.Should().BeFalse();

        // Stated, not left out. The entity defaults to notifying, but the create request carries the
        // flag, so an omitted value binds as false and the sync would drop the event in silence.
        var wishlistResponse = await AuthenticatedPostAsync(
            accessToken,
            "/api/wishlist",
            new
            {
                storeItemId = item.Id,
                notifyOnAvailable = true,
                autoRedeem = false,
                redemptionMessage = (string?)null,
            }
        );
        wishlistResponse.IsSuccessStatusCode.Should().BeTrue();

        // Back in stock. Only the current quantity moves — availability is the whole point here.
        _seApi
            .GetStoreItemsAsync(new StoreExternalId(ChannelId), Arg.Any<CancellationToken>())
            .Returns([CreateSeItem(itemName, enabled: true, quantity: 3)]);

        return accessToken;
    }

    private async Task<string> RegisterAndSignInAsync()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = UserEmail, password = UserPassword }
        );
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = UserEmail, password = UserPassword }
        );
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        var accessToken = login!.AccessToken;

        // Stubbed before the credential is saved: the save checks it and records the account name.
        _seApi
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("se-username");
        await AuthenticatedPutAsync(
            accessToken,
            "/api/users/me/credentials/streamelements",
            new { secret = "fake-se-token" }
        );

        return accessToken;
    }

    /// <summary>
    /// Empties the address columns straight through the context. <c>UserManager</c> cannot do it:
    /// Identity's own validator rejects a blank user name before the single-user rule is reached.
    /// This is setup rather than an assertion, so it takes the shortest correct route.
    /// </summary>
    private async Task ClearIdentityColumnsAsync(bool clearUserName)
    {
        using var scope = _factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        await identity.Users.ExecuteUpdateAsync(setters =>
            setters
                .SetProperty(u => u.Email, (string?)null)
                .SetProperty(u => u.NormalizedEmail, (string?)null)
        );

        if (clearUserName)
        {
            await identity.Users.ExecuteUpdateAsync(setters =>
                setters
                    .SetProperty(u => u.UserName, (string?)null)
                    .SetProperty(u => u.NormalizedUserName, (string?)null)
            );
        }
    }

    private async Task<SyncResult> RunSyncAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var syncStores = scope.ServiceProvider.GetRequiredService<SyncStores>();
        return await syncStores.ExecuteAsync(CancellationToken.None);
    }

    /// <summary>
    /// The email notification for the cycle. Filtered by channel because the coverage phase writes
    /// in-app notifications to the same table.
    /// </summary>
    private async Task<Notification> SingleEmailNotificationAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db
            .Notifications.Include(n => n.Items)
            .Where(n => n.Channel == NotificationChannel.Email)
            .SingleAsync();
    }

    private static ProviderStoreItem CreateSeItem(string name, bool enabled, int quantity) =>
        new(
            Provider: StoreProvider.StreamElements,
            Id: new ProviderItemId("se-notify-1"),
            Store: new StoreExternalId(ChannelId),
            Name: name,
            Description: null,
            Type: "perk",
            Cost: 1500,
            CategoryName: "default",
            Enabled: enabled,
            Featured: false,
            Public: true,
            SubscriberOnly: false,
            AllowMessages: false,
            UserInputDefinitions: [],
            Sources: [],
            Quantity: new ItemQuantity(5, quantity),
            Cooldown: new ItemCooldown(0, 0, 0),
            CreatedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt: null
        );

    private Task<HttpResponseMessage> AuthenticatedGetAsync(string token, string url) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, url), token);

    private Task<HttpResponseMessage> AuthenticatedPostAsync(
        string token,
        string url,
        object payload
    ) => SendAsync(WithBody(HttpMethod.Post, url, payload), token);

    private Task<HttpResponseMessage> AuthenticatedPutAsync(
        string token,
        string url,
        object payload
    ) => SendAsync(WithBody(HttpMethod.Put, url, payload), token);

    private Task<HttpResponseMessage> AuthenticatedPatchAsync(
        string token,
        string url,
        object payload
    ) => SendAsync(WithBody(HttpMethod.Patch, url, payload), token);

    private static HttpRequestMessage WithBody(HttpMethod method, string url, object payload) =>
        new(method, url) { Content = JsonContent.Create(payload) };

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private sealed record LoginResponse(string AccessToken);

    private sealed record StoreDetail(Guid Id);

    private sealed record StoreItemPage(StoreItemRow[] Items);

    private sealed record StoreItemRow(Guid Id, string Name, long Cost, bool IsAvailable);

    #endregion
}
