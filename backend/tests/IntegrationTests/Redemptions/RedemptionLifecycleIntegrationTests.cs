using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Notifications;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Redemptions;

/// <summary>
/// The submit → poll → settle path, against a real database.
/// <para>
/// This path had no database-backed coverage at all, and that is how it shipped broken: the
/// completion lookup threw on every poll, the error was swallowed as if the redemption were merely
/// unfinished upstream, and every row aged out on the completion timeout instead. Nothing in the
/// suite followed a redemption past <c>Submitted</c> through a real store, so nothing noticed that
/// no redemption had ever reached <c>Completed</c>.
/// </para>
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class RedemptionLifecycleIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly IStoreProviderClient _mockSeApi;
    private readonly IEmailNotifier _mockEmailService;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private const string ChannelId = "ch-1";

    public RedemptionLifecycleIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockSeApi = Substitute.For<IStoreProviderClient>();
        _mockEmailService = Substitute.For<IEmailNotifier>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString)
        {
            MockStreamElementsApi = _mockSeApi,
            MockEmailService = _mockEmailService,
        };
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// The whole point: a redemption that StreamElements finishes has to land as
    /// <c>Completed</c>, with the upstream settle time recorded.
    /// </summary>
    [Fact]
    public async Task An_auto_redeemed_item_is_submitted_and_then_completed_by_polling()
    {
        await SetupAutoRedeemableItemAsync();

        await RunSyncAsync();
        (await LoadSingleRedemptionAsync()).Status.Should().Be(RedemptionStatus.Pending);

        StubRedeemAccepted();
        await RunSubmitAsync();

        var submitted = await LoadSingleRedemptionAsync();
        submitted.Status.Should().Be(RedemptionStatus.Submitted);
        submitted.ProviderRedemptionId.Should().Be("se-redemption-1");

        var completedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        StubLookup(completed: true, updatedAt: completedAt);
        await RunPollAsync();

        var completed = await LoadSingleRedemptionAsync();
        completed.Status.Should().Be(RedemptionStatus.Completed);
        completed.SettledAt.Should().Be(completedAt);
        completed.PollFailures.Should().Be(0);
    }

    /// <summary>
    /// A completion check that keeps throwing must leave the status alone — the submission did
    /// succeed — while the failure count persists, so a wedged row is visible instead of looking
    /// like one still waiting upstream.
    /// </summary>
    [Fact]
    public async Task A_failing_completion_check_is_counted_and_persisted_without_changing_status()
    {
        await SetupAutoRedeemableItemAsync();
        await RunSyncAsync();
        StubRedeemAccepted();
        await RunSubmitAsync();

        _mockSeApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(
                new ProviderApiException(
                    ProviderApiException.NoResponse,
                    null,
                    "An error occured deserializing the response."
                )
            );

        await RunPollAsync();
        await RunPollAsync();
        await RunPollAsync();

        var stuck = await LoadSingleRedemptionAsync();
        stuck.Status.Should().Be(RedemptionStatus.Submitted);
        stuck.FailureCode.Should().BeNull();
        // Read back from Postgres: the counter has to survive the delta write, not just the entity.
        stuck.PollFailures.Should().Be(3);
        stuck.LastPollError.Should().Contain("deserializing");
    }

    /// <summary>
    /// A reading that gets through clears the trail, so a recovered outage does not leave the row
    /// permanently marked as failing.
    /// </summary>
    [Fact]
    public async Task A_recovered_completion_check_clears_the_persisted_failure_trail()
    {
        await SetupAutoRedeemableItemAsync();
        await RunSyncAsync();
        StubRedeemAccepted();
        await RunSubmitAsync();

        _mockSeApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .ThrowsAsync(
                new ProviderApiException(ProviderApiException.NoResponse, null, "upstream down")
            );
        await RunPollAsync();
        (await LoadSingleRedemptionAsync()).PollFailures.Should().Be(1);

        StubLookup(completed: false, updatedAt: DateTimeOffset.UtcNow);
        await RunPollAsync();

        var recovered = await LoadSingleRedemptionAsync();
        recovered.Status.Should().Be(RedemptionStatus.Submitted);
        recovered.PollFailures.Should().Be(0);
        recovered.LastPollError.Should().BeNull();
    }

    /// <summary>
    /// A refusal arrives as <c>rejected: true</c> next to <c>completed: false</c>. Read without the
    /// flag it is indistinguishable from work still pending, so the row waited out the completion
    /// timeout and was then blamed on a timeout it never had.
    /// </summary>
    [Fact]
    public async Task A_rejection_upstream_settles_the_redemption_as_rejected()
    {
        await SetupAutoRedeemableItemAsync();
        await RunSyncAsync();
        StubRedeemAccepted();
        await RunSubmitAsync();

        var rejectedAt = new DateTimeOffset(2026, 5, 2, 9, 30, 0, TimeSpan.Zero);
        StubLookup(completed: false, updatedAt: rejectedAt, rejected: true);
        await RunPollAsync();

        var rejected = await LoadSingleRedemptionAsync();
        rejected.Status.Should().Be(RedemptionStatus.Rejected);
        rejected.SettledAt.Should().Be(rejectedAt);
    }

    /// <summary>
    /// A submitted row blocks auto-redeem for its wishlist entry, so one that can never settle
    /// takes the feature down with it. Only reaching a terminal state releases the entry.
    /// </summary>
    [Fact]
    public async Task A_settled_redemption_stops_blocking_further_auto_redeems()
    {
        await SetupAutoRedeemableItemAsync();
        await RunSyncAsync();
        StubRedeemAccepted();
        await RunSubmitAsync();

        await AssertInflightAsync(true);

        StubLookup(completed: false, updatedAt: DateTimeOffset.UtcNow, rejected: true);
        await RunPollAsync();

        await AssertInflightAsync(false);
    }

    /// <summary>
    /// A code comes back once. It has to be on disk, encrypted, by the time the redemption is, and
    /// then reach the owner exactly once however often the delivery runs.
    /// </summary>
    [Fact]
    public async Task A_returned_gift_card_code_is_stored_encrypted_and_emailed_once()
    {
        await SetupAutoRedeemableItemAsync();
        await RunSyncAsync();
        _mockSeApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProviderRedemptionResult.Accepted(null, true, "GIFT-1234-ABCD"));

        await RunSubmitAsync();

        (await LoadSingleRedemptionAsync()).Status.Should().Be(RedemptionStatus.Completed);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.FulfillmentCodes.SingleAsync();
            stored.EncryptedCode.Should().NotContain("GIFT-1234-ABCD");
            stored.SentAt.Should().BeNull();
        }

        await RunDeliverAsync();
        await RunDeliverAsync();

        await _mockEmailService
            .Received(1)
            .SendFulfillmentCodeAsync(
                "redeem@test.com",
                Arg.Is<FulfillmentCodeEmailModel>(m => m.Code == "GIFT-1234-ABCD"),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.FulfillmentCodes.SingleAsync()).SentAt.Should().NotBeNull();
        }
    }

    private async Task RunDeliverAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<DeliverFulfillmentCodes>()
            .ExecuteAsync(default);
    }

    private async Task AssertInflightAsync(bool expected)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entryId = await db.Redemptions.Select(r => r.WishlistEntryId).FirstAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IRedemptionRepository>();
        var usage = await repo.CountHoldingQuotaAsync([entryId], CancellationToken.None);
        (usage.GetValueOrDefault(entryId).Inflight > 0).Should().Be(expected);
    }

    private void StubRedeemAccepted() =>
        _mockSeApi
            .RedeemAsync(
                Arg.Any<ProviderRedeemCommand>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new ProviderRedemptionResult.Accepted(
                    "se-redemption-1",
                    Completed: false,
                    FulfillmentCode: null
                )
            );

    private void StubLookup(bool completed, DateTimeOffset updatedAt, bool rejected = false) =>
        _mockSeApi
            .GetRedemptionAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProviderRedemptionStatus(completed, rejected, updatedAt));

    private async Task<Redemption> LoadSingleRedemptionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Redemptions.SingleAsync();
    }

    private async Task RunSyncAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SyncStores>().ExecuteAsync(default);
    }

    private async Task RunSubmitAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SubmitRedemptions>().ExecuteAsync(default);
    }

    private async Task RunPollAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<PollRedemptionStatus>()
            .ExecuteAsync(default);
    }

    /// <summary>
    /// Registers the owner, stores an SE token, creates a store, then wishlists its one available
    /// item with auto-redeem on. The item takes no user input, so submission has nothing to resolve
    /// from the store profile.
    /// </summary>
    private async Task SetupAutoRedeemableItemAsync()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "redeem@test.com", password = "Password123!" }
        );
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "redeem@test.com", password = "Password123!" }
        );
        var accessToken = (
            await loginResponse.Content.ReadFromJsonAsync<LoginResponse>()
        )!.AccessToken;

        // Stubbed before the credential is saved: the save checks it and records the account name.
        _mockSeApi
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("se-username");
        await SendAsync(
            HttpMethod.Put,
            accessToken,
            "/api/users/me/credentials/streamelements",
            new { secret = "t" }
        );
        _mockSeApi
            .GetPointsAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1_000_000L);

        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId(ChannelId), Arg.Any<CancellationToken>())
            .Returns([AvailableItem()]);

        await SendAsync(
            HttpMethod.Post,
            accessToken,
            "/api/stores",
            new { channelId = ChannelId, displayName = "Store One" }
        );

        // The store create seeds the catalogue, so the item row exists to wishlist against.
        Guid storeItemId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            storeItemId = await db
                // Compared as the value object: the converter maps the whole type, so reaching through
                // .Value gives EF an expression it cannot translate.
                .StoreItems.Where(i => i.ProviderItemId == new ProviderItemId("se-1"))
                .Select(i => i.Id)
                .SingleAsync();
        }

        await SendAsync(
            HttpMethod.Post,
            accessToken,
            "/api/wishlist",
            new { storeItemId, autoRedeem = true }
        );

        _mockSeApi.ClearReceivedCalls();
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId(ChannelId), Arg.Any<CancellationToken>())
            .Returns([AvailableItem()]);
        _mockSeApi
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("se-username");
        _mockSeApi
            .GetPointsAsync(
                Arg.Any<StoreExternalId>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(1_000_000L);
    }

    private static ProviderStoreItem AvailableItem() =>
        new(
            Provider: StoreProvider.StreamElements,
            Id: new ProviderItemId("se-1"),
            Store: new StoreExternalId(ChannelId),
            Name: "Auto Item",
            Description: null,
            Type: "perk",
            Cost: 100,
            CategoryName: "default",
            Enabled: true,
            Featured: false,
            Public: true,
            SubscriberOnly: false,
            AllowMessages: false,
            UserInputDefinitions: [],
            Sources: [],
            Quantity: new ItemQuantity(-1, -1),
            Cooldown: new ItemCooldown(0, 0, 0),
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: null
        );

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string token,
        string url,
        object payload
    )
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn
    );
}
