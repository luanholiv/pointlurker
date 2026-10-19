using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace IntegrationTests.Stores;

[Collection(ContainersCollection.Name)]
public sealed class StoreEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly IStoreProviderClient _mockSeApi;
    private readonly IStoreProviderClient _mockGauPoints = Substitute.For<IStoreProviderClient>();
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public StoreEndpointTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _mockSeApi = Substitute.For<IStoreProviderClient>();
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString)
        {
            MockStreamElementsApi = _mockSeApi,
            MockGauPointsApi = _mockGauPoints,
        };
        _client = _factory.CreateClient();
        await RegisterAndLoginAsync();
        await SetStreamElementsTokenAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task CreateStore_returns_created_with_items()
    {
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("test-channel"), Arg.Any<CancellationToken>())
            .Returns([
                new ProviderStoreItem(
                    Provider: StoreProvider.StreamElements,
                    Id: new ProviderItemId("se-item-1"),
                    Store: new StoreExternalId("test-channel"),
                    Name: "Test Item",
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
                    UpdatedAt: null,
                    ThumbnailUrl: "https://cdn.example.com/thumb.png",
                    PreviewUrl: "https://cdn.example.com/preview.png"
                ),
            ]);
        _mockSeApi
            .LookupStoreAsync("test-channel", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("test-channel"),
                    "teststore",
                    "Test Store",
                    "https://cdn.example.com/avatar.png"
                )
            );

        var response = await AuthenticatedPostAsync(
            "/api/stores",
            new { channelId = "test-channel", displayName = "Test Store" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<StoreDetailResponse>();
        body.Should().NotBeNull();
        body!.DisplayName.Should().Be("Test Store");
        body.ChannelId.Should().Be("test-channel");
        body.AvatarUrl.Should().Be("https://cdn.example.com/avatar.png");
        body.ItemCount.Should().Be(1);

        // The created store's items are served by their own endpoint now, so the response reports
        // how many were ingested and this reads them back to check what was ingested.
        // `previewUrl` is not part of an item row — the grid never renders it — and its persistence
        // is covered by StoreImageUrlPersistenceTests.Image_urls_round_trip.
        var items = await GetStoreItemsAsync(body.Id);
        items.Should().HaveCount(1);
        items[0].Name.Should().Be("Test Item");
        items[0].ThumbnailUrl.Should().Be("https://cdn.example.com/thumb.png");
    }

    [Fact]
    public async Task CreateStore_succeeds_with_null_avatar_when_channel_lookup_fails()
    {
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId("test-channel"), Arg.Any<CancellationToken>())
            .Returns([]);
        _mockSeApi
            .LookupStoreAsync("test-channel", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(500, "Server Error", "boom"));

        var response = await AuthenticatedPostAsync(
            "/api/stores",
            new { channelId = "test-channel", displayName = "Lookup Fails Store" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<StoreDetailResponse>();
        body.Should().NotBeNull();
        body!.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task CreateStore_returns_403_when_no_se_token()
    {
        await ClearStreamElementsTokenAsync();

        var response = await AuthenticatedPostAsync(
            "/api/stores",
            new { channelId = "any-channel", displayName = "No Token Store" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await ParseProblemDetailsAsync(response);
        problem
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.Auth.ProviderCredentialNotConfigured);
        problem
            .GetProperty("params")
            .GetProperty("provider")
            .GetString()
            .Should()
            .Be("streamelements");
    }

    [Fact]
    public async Task A_gaupoints_store_resolves_its_identity_server_side()
    {
        await SetGauPointsSessionAsync();
        StubGauPointsStore();

        var response = await AuthenticatedPostAsync("/api/stores", new { provider = "gaupoints" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<StoreDetailResponse>();
        body!.ChannelId.Should().Be("gaupoints");
        body.DisplayName.Should().Be("GauPoints");
        body.AvatarUrl.Should().Be("https://gaupoints.com.br/logo.png");
        body.Provider.Should().Be("gaupoints");
        await _mockSeApi
            .DidNotReceive()
            .GetStoreItemsAsync(Arg.Any<StoreExternalId>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The gate is the provider's own credential. A StreamElements token, which this fixture holds,
    /// says nothing about whether GauPoints can be used.
    /// </summary>
    [Fact]
    public async Task A_gaupoints_store_needs_the_gaupoints_session_not_the_se_token()
    {
        StubGauPointsStore();

        var response = await AuthenticatedPostAsync("/api/stores", new { provider = "gaupoints" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await ParseProblemDetailsAsync(response);
        problem
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.Auth.ProviderCredentialNotConfigured);
        problem.GetProperty("params").GetProperty("provider").GetString().Should().Be("gaupoints");
    }

    [Fact]
    public async Task A_client_sent_identity_for_gaupoints_is_refused()
    {
        await SetGauPointsSessionAsync();

        var response = await AuthenticatedPostAsync(
            "/api/stores",
            new
            {
                provider = "gaupoints",
                channelId = "someone-else",
                displayName = "Fake",
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await ParseProblemDetailsAsync(response))
            .GetProperty("params")
            .GetProperty("errors");
        errors.GetArrayLength().Should().Be(2);
        errors[0]
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.Stores.IdentityNotAccepted);
    }

    [Fact]
    public async Task A_second_gaupoints_store_is_a_conflict()
    {
        await SetGauPointsSessionAsync();
        StubGauPointsStore();
        await AuthenticatedPostAsync("/api/stores", new { provider = "gaupoints" });

        var response = await AuthenticatedPostAsync("/api/stores", new { provider = "gaupoints" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ParseProblemDetailsAsync(response))
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.Stores.AlreadyExists);
    }

    [Fact]
    public async Task An_unreachable_gaupoints_answers_502_naming_it()
    {
        await SetGauPointsSessionAsync();
        _mockGauPoints
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(
                new ProviderApiException(ProviderApiException.NoResponse, null, "refused")
            );

        var response = await AuthenticatedPostAsync("/api/stores", new { provider = "gaupoints" });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var problem = await ParseProblemDetailsAsync(response);
        problem.GetProperty("code").GetString().Should().Be(ErrorCodes.ProviderApi.FetchFailed);
        problem.GetProperty("params").GetProperty("provider").GetString().Should().Be("gaupoints");
    }

    [Fact]
    public async Task ListStores_returns_ok_with_stores()
    {
        var response = await AuthenticatedGetAsync("/api/stores");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStore_returns_404_for_nonexistent()
    {
        var response = await AuthenticatedGetAsync($"/api/stores/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteStore_returns_404_for_nonexistent()
    {
        var response = await AuthenticatedDeleteAsync($"/api/stores/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_profile_values_removes_the_value()
    {
        var storeId = await CreateStoreWithoutItemsAsync("delete-value-channel");
        await AuthenticatedPutAsync(
            $"/api/stores/{storeId}/profile/values",
            new
            {
                values = new[] { new { labelCanonical = "Steam ID", value = "76561198000000000" } },
            }
        );

        var response = await AuthenticatedDeleteAsync(
            $"/api/stores/{storeId}/profile/values?label={Uri.EscapeDataString("Steam ID")}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var profile = await GetProfileAsync(storeId);
        profile.Resolved.Should().BeEmpty();

        // Same request again: idempotent, still 204.
        var repeat = await AuthenticatedDeleteAsync(
            $"/api/stores/{storeId}/profile/values?label={Uri.EscapeDataString("Steam ID")}"
        );
        repeat.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Deletes_a_value_whose_label_requires_url_encoding()
    {
        // Slashes, spaces and parentheses in one label: the case a path parameter would need a
        // catch-all for, and the reason the label travels in the query string.
        const string label = "Discord tag / handle (IGN)";
        var storeId = await CreateStoreWithoutItemsAsync("encoded-label-channel");
        await AuthenticatedPutAsync(
            $"/api/stores/{storeId}/profile/values",
            new { values = new[] { new { labelCanonical = label, value = "someone#1234" } } }
        );
        (await GetProfileAsync(storeId)).CanonicalLabels.Should().Contain(label);

        var response = await AuthenticatedDeleteAsync(
            $"/api/stores/{storeId}/profile/values?label={Uri.EscapeDataString(label)}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetProfileAsync(storeId)).CanonicalLabels.Should().BeEmpty();
    }

    [Fact]
    public async Task DELETE_profile_aliases_removes_the_alias()
    {
        var storeId = await CreateStoreWithoutItemsAsync("delete-alias-channel");
        await AuthenticatedPutAsync(
            $"/api/stores/{storeId}/profile/values",
            new { values = new[] { new { labelCanonical = "Twitch Username", value = "someone" } } }
        );
        await AuthenticatedPostAsync(
            $"/api/stores/{storeId}/profile/aliases",
            new { rawLabel = "Nick (IGN)", labelCanonical = "Twitch Username" }
        );

        var response = await AuthenticatedDeleteAsync(
            $"/api/stores/{storeId}/profile/aliases?rawLabel={Uri.EscapeDataString("Nick (IGN)")}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        // The value survives — only the mapping is detached.
        (await GetProfileAsync(storeId))
            .CanonicalLabels.Should()
            .Equal("Twitch Username");
    }

    [Fact]
    public async Task DELETE_profile_values_returns_404_for_nonexistent_store()
    {
        var response = await AuthenticatedDeleteAsync(
            $"/api/stores/{Guid.NewGuid()}/profile/values?label=Steam%20ID"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateStoreWithoutItemsAsync(string channelId)
    {
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId(channelId), Arg.Any<CancellationToken>())
            .Returns([]);
        _mockSeApi
            .LookupStoreAsync(channelId, Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(new StoreExternalId(channelId), channelId, channelId, null)
            );

        var response = await AuthenticatedPostAsync(
            "/api/stores",
            new { channelId, displayName = channelId }
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<StoreDetailResponse>();
        return body!.Id;
    }

    private async Task<StoreProfileResponse> GetProfileAsync(Guid storeId)
    {
        var response = await AuthenticatedGetAsync($"/api/stores/{storeId}/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<StoreProfileResponse>())!;
    }

    private async Task RegisterAndLoginAsync()
    {
        var registerPayload = new { email = "test@example.com", password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        var loginPayload = new { email = "test@example.com", password = "Password123!" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginPayload);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        _accessToken = loginBody!.AccessToken;
    }

    private async Task SetStreamElementsTokenAsync()
    {
        await AuthenticatedPutAsync(
            "/api/users/me/credentials/streamelements",
            new { secret = "fake-se-token" }
        );
    }

    private async Task SetGauPointsSessionAsync()
    {
        _mockGauPoints
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("someviewer");
        var response = await AuthenticatedPutAsync(
            "/api/users/me/credentials/gaupoints",
            new { secret = "fake-session" }
        );
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private void StubGauPointsStore()
    {
        _mockGauPoints
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("gaupoints"),
                    "gaupoints",
                    "GauPoints",
                    "https://gaupoints.com.br/logo.png",
                    ItemCount: 0
                )
            );
        _mockGauPoints
            .GetStoreItemsAsync(new StoreExternalId("gaupoints"), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private static async Task<JsonElement> ParseProblemDetailsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private async Task ClearStreamElementsTokenAsync()
    {
        await AuthenticatedDeleteAsync("/api/users/me/credentials/streamelements");
    }

    /// <summary>
    /// <c>avail=0</c> turns the default availability filter off so a fixture's unavailable items
    /// are visible to assertions.
    /// </summary>
    private async Task<StoreItemRowResponse[]> GetStoreItemsAsync(Guid storeId)
    {
        var response = await AuthenticatedGetAsync(
            $"/api/stores/{storeId}/items?avail=0&limit=200"
        );
        var page = await response.Content.ReadFromJsonAsync<StoreItemPageResponse>();
        return page!.Items;
    }

    private async Task<HttpResponseMessage> AuthenticatedGetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AuthenticatedPutAsync(string url, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AuthenticatedPostAsync(string url, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> AuthenticatedDeleteAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn
    );

    private sealed record StoreDetailResponse(
        Guid Id,
        string ChannelId,
        string DisplayName,
        bool IsActive,
        DateTimeOffset? LastSyncedAt,
        DateTimeOffset CreatedAt,
        int ItemCount,
        string? AvatarUrl,
        string Provider
    );

    private sealed record StoreItemPageResponse(
        StoreItemRowResponse[] Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record StoreItemRowResponse(Guid Id, string Name, string? ThumbnailUrl);

    private sealed record StoreProfileResponse(
        StoreProfileUnresolvedInputResponse[] Unresolved,
        StoreProfileResolvedInputResponse[] Resolved,
        string[] CanonicalLabels
    );

    private sealed record StoreProfileUnresolvedInputResponse(
        string RawLabel,
        string? BestCanonical,
        int BestScore,
        string? AliasTarget,
        int ItemCount,
        int BlockedEntryCount
    );

    private sealed record StoreProfileResolvedInputResponse(
        string LabelCanonical,
        DateTimeOffset UpdatedAt,
        StoreProfileRequesterResponse[] RequestedBy
    );

    private sealed record StoreProfileRequesterResponse(
        string RawLabel,
        string Source,
        int Confidence,
        int ItemCount
    );

    private sealed record StoreItemDetailResponse(
        Guid Id,
        string ProviderItemId,
        string Name,
        string? Description,
        string Type,
        long Cost,
        string CategoryName,
        bool Enabled,
        bool Featured,
        bool IsPublic,
        bool SubscriberOnly,
        bool AllowMessages,
        string[] UserInputDefinitions,
        int QuantityTotal,
        int QuantityCurrent,
        bool IsAvailable,
        Guid? WishlistEntryId,
        string? ThumbnailUrl,
        string? PreviewUrl
    );
}
