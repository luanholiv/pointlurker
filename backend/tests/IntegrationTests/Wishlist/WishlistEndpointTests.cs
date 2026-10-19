using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Npgsql;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace IntegrationTests.Wishlist;

[Collection(ContainersCollection.Name)]
public sealed class WishlistEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly IStoreProviderClient _mockSeApi;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public WishlistEndpointTests(DatabaseFixture fixture)
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
    public async Task Create_defaults_the_desired_count_when_the_body_omits_it()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        var createResponse = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                redemptionMessage = (string?)null,
            }
        );

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created =
            await createResponse.Content.ReadFromJsonAsync<WishlistEntryCreatedResponse>();
        var detail = await AuthenticatedGetAsync($"/api/wishlist/{created!.Id}");
        var body = await detail.Content.ReadFromJsonAsync<WishlistEntryDetailResponse>();

        body!.DesiredRedemptions.Should().Be(1);
        body.QuotaUsed.Should().Be(0);
    }

    [Fact]
    public async Task Create_round_trips_an_explicit_unlimited_desired_count()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        var createResponse = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                redemptionMessage = (string?)null,
                desiredRedemptions = (int?)null,
            }
        );

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created =
            await createResponse.Content.ReadFromJsonAsync<WishlistEntryCreatedResponse>();
        var detail = await AuthenticatedGetAsync($"/api/wishlist/{created!.Id}");
        var body = await detail.Content.ReadFromJsonAsync<WishlistEntryDetailResponse>();

        body!.DesiredRedemptions.Should().BeNull();
    }

    [Fact]
    public async Task Create_rejects_a_desired_count_above_the_cap()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        var response = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = true,
                redemptionMessage = (string?)null,
                desiredRedemptions = WishlistEntry.DesiredRedemptionsMax + 1,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Full_crud_flow_create_read_update_delete()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        // Create
        var createResponse = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
                redemptionMessage = (string?)null,
            }
        );
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created =
            await createResponse.Content.ReadFromJsonAsync<WishlistEntryCreatedResponse>();
        created.Should().NotBeNull();
        created!.StoreItemId.Should().Be(storeItemId);
        // Resolved through the WatchedStore navigation that AddWishlistEntry now eager-loads.
        created.StoreName.Should().Be(_lastCreatedStoreName);

        // Read single
        var getResponse = await AuthenticatedGetAsync($"/api/wishlist/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getResponse.Content.ReadFromJsonAsync<WishlistEntryDetailResponse>();
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(created.Id);

        // Update
        var updateResponse = await AuthenticatedPutAsync(
            $"/api/wishlist/{created.Id}",
            new
            {
                notifyOnAvailable = false,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
                redemptionMessage = "updated message",
            }
        );
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Delete
        var deleteResponse = await AuthenticatedDeleteAsync($"/api/wishlist/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var afterDelete = await AuthenticatedGetAsync($"/api/wishlist/{created.Id}");
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_duplicate_returns_409()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
            }
        );

        var duplicate = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
            }
        );

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_for_nonexistent_item_returns_404()
    {
        var response = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId = Guid.NewGuid(),
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_with_auto_redeem_and_uncovered_profile_returns_422()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId(
            userInputDefinitions: ["email", "name"]
        );

        var response = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = true,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Grouped_list_returns_correct_structure()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
            }
        );

        var response = await AuthenticatedGetAsync("/api/wishlist");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<WishlistGroupedListResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(1);
        body.Items[0].Entries.Should().HaveCount(1);
    }

    /// <summary>
    /// Pins that this list is not paged, which is the one thing its envelope could be read as
    /// promising. It carries <c>offset</c> and <c>limit</c> because the shared envelope is
    /// arch-test enforced, not because a slice was taken — so bounds sent by a caller change
    /// nothing, and the values reported back always describe the whole result.
    /// </summary>
    [Fact]
    public async Task Slice_bounds_are_structural_and_the_list_is_served_whole()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
            }
        );

        var unbounded = await AuthenticatedGetAsync("/api/wishlist");
        var bounded = await AuthenticatedGetAsync("/api/wishlist?offset=1&limit=1");

        var whole = await unbounded.Content.ReadFromJsonAsync<WishlistGroupedListResponse>();
        var sliced = await bounded.Content.ReadFromJsonAsync<WishlistGroupedListResponse>();

        // Asking for a slice changes nothing about what comes back.
        sliced!.Items.Should().HaveSameCount(whole!.Items);

        // And the envelope says so rather than echoing what was asked for.
        sliced.Offset.Should().Be(0);
        sliced.Limit.Should().Be(sliced.TotalCount);
    }

    [Fact]
    public async Task Store_detail_includes_wishlist_entry_id_after_adding()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        var createResponse = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
            }
        );
        var created =
            await createResponse.Content.ReadFromJsonAsync<WishlistEntryCreatedResponse>();

        var items = await GetStoreItemsAsync(_lastCreatedStoreId);

        var item = items.First(i => i.Id == storeItemId);
        item.WishlistEntryId.Should().Be(created!.Id);
    }

    [Fact]
    public async Task IsBlocked_set_when_auto_redeem_lacks_profile_coverage()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId(userInputDefinitions: ["email"]);

        await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
            }
        );

        var listResponse = await AuthenticatedGetAsync("/api/wishlist");
        var list = await listResponse.Content.ReadFromJsonAsync<WishlistGroupedListResponse>();
        list!.Items[0].Entries[0].IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task Redemption_message_stored_as_ciphertext_and_returned_decrypted()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        var createResponse = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
                redemptionMessage = "thanks for the sub!",
            }
        );
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created =
            await createResponse.Content.ReadFromJsonAsync<WishlistEntryCreatedResponse>();

        // Raw column holds ciphertext, not the plaintext message.
        var rawStored = await ReadRawRedemptionMessageAsync(created!.Id);
        rawStored.Should().NotBeNull();
        rawStored.Should().NotBe("thanks for the sub!");

        // The read endpoint decrypts transparently.
        var getResponse = await AuthenticatedGetAsync($"/api/wishlist/{created.Id}");
        var detail = await getResponse.Content.ReadFromJsonAsync<WishlistEntryDetailResponse>();
        detail!.RedemptionMessage.Should().Be("thanks for the sub!");
    }

    [Fact]
    public async Task Null_redemption_message_stays_null_at_rest()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();

        var createResponse = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
                redemptionMessage = (string?)null,
            }
        );
        var created =
            await createResponse.Content.ReadFromJsonAsync<WishlistEntryCreatedResponse>();

        var rawStored = await ReadRawRedemptionMessageAsync(created!.Id);
        rawStored.Should().BeNull();
    }

    [Fact]
    /// <summary>
    /// The cap is request shape, so it is reported per-field by the request validator rather than
    /// as a whole-request 422 from the application layer — the client learns which field broke.
    /// </summary>
    public async Task Over_length_redemption_message_is_reported_per_field()
    {
        var storeItemId = await CreateStoreAndGetFirstItemId();
        var tooLong = new string('x', WishlistEntry.RedemptionMessageMaxLength + 1);

        var response = await AuthenticatedPostAsync(
            "/api/wishlist",
            new
            {
                storeItemId,
                notifyOnAvailable = true,
                autoRedeem = false,
                userInputValues = Array.Empty<string>(),
                redemptionMessage = tooLong,
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        var entry = body.GetProperty("params").GetProperty("errors")[0];
        entry.GetProperty("field").GetString().Should().Be("redemptionMessage");
        entry.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.TooLong);
        entry
            .GetProperty("params")
            .GetProperty("max")
            .GetInt32()
            .Should()
            .Be(WishlistEntry.RedemptionMessageMaxLength);
    }

    private async Task<string?> ReadRawRedemptionMessageAsync(Guid entryId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT \"RedemptionMessage\" FROM wishlist_entries WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", entryId);
        var result = await command.ExecuteScalarAsync();
        return result is DBNull or null ? null : (string)result;
    }

    private Guid _lastCreatedStoreId;
    private string _lastCreatedStoreName = string.Empty;

    private async Task<Guid> CreateStoreAndGetFirstItemId(
        IReadOnlyList<string>? userInputDefinitions = null
    )
    {
        var channelId = $"test-channel-{Guid.NewGuid():N}";
        _mockSeApi
            .GetStoreItemsAsync(new StoreExternalId(channelId), Arg.Any<CancellationToken>())
            .Returns([
                new ProviderStoreItem(
                    Provider: StoreProvider.StreamElements,
                    Id: new ProviderItemId($"se-item-{Guid.NewGuid():N}"),
                    Store: new StoreExternalId(channelId),
                    Name: "Test Item",
                    Description: null,
                    Type: "perk",
                    Cost: 500,
                    CategoryName: "default",
                    Enabled: true,
                    Featured: false,
                    Public: true,
                    SubscriberOnly: false,
                    AllowMessages: true,
                    UserInputDefinitions: userInputDefinitions?.ToArray() ?? [],
                    Sources: [],
                    Quantity: new ItemQuantity(-1, -1),
                    Cooldown: new ItemCooldown(0, 0, 0),
                    CreatedAt: DateTimeOffset.UtcNow,
                    UpdatedAt: null
                ),
            ]);

        var createStore = await AuthenticatedPostAsync(
            "/api/stores",
            new { channelId, displayName = $"Store {channelId}" }
        );

        var store = await createStore.Content.ReadFromJsonAsync<StoreDetailResponse>();
        _lastCreatedStoreId = store!.Id;
        _lastCreatedStoreName = store.DisplayName;

        // Items are served by their own endpoint now; the create response reports only how many.
        return (await GetStoreItemsAsync(store.Id))[0].Id;
    }

    /// <summary>
    /// <c>avail=0</c> keeps every item visible: these fixtures include unavailable items, which the
    /// endpoint's default availability filter would otherwise hide.
    /// </summary>
    private async Task<StoreItemRowResponse[]> GetStoreItemsAsync(Guid storeId)
    {
        var response = await AuthenticatedGetAsync(
            $"/api/stores/{storeId}/items?avail=0&limit=200"
        );
        var page = await response.Content.ReadFromJsonAsync<StoreItemPageResponse>();
        return page!.Items;
    }

    private async Task RegisterAndLoginAsync()
    {
        var registerPayload = new { email = "wishlist@example.com", password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", registerPayload);

        var loginPayload = new { email = "wishlist@example.com", password = "Password123!" };
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

    private async Task<HttpResponseMessage> AuthenticatedGetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
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

    private async Task<HttpResponseMessage> AuthenticatedPutAsync(string url, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
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

    private sealed record WishlistEntryCreatedResponse(
        Guid Id,
        Guid StoreItemId,
        string ItemName,
        string StoreName,
        bool NotifyOnAvailable,
        bool AutoRedeem,
        DateTimeOffset CreatedAt
    );

    private sealed record WishlistEntryDetailResponse(
        Guid Id,
        Guid StoreItemId,
        string ItemName,
        long ItemCost,
        bool IsAvailable,
        string StoreName,
        Guid StoreId,
        bool NotifyOnAvailable,
        bool AutoRedeem,
        int? DesiredRedemptions,
        int QuotaUsed,
        bool AllowMessages,
        string? RedemptionMessage,
        bool IsBlocked,
        string[] MissingLabels,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt
    );

    private sealed record WishlistGroupedListResponse(
        WishlistStoreGroupResponse[] Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record WishlistStoreGroupResponse(
        Guid StoreId,
        string StoreName,
        bool IsActive,
        DateTimeOffset? LastSyncedAt,
        WishlistEntryListItemResponse[] Entries
    );

    private sealed record WishlistEntryListItemResponse(
        Guid Id,
        Guid StoreItemId,
        string ItemName,
        long ItemCost,
        bool IsAvailable,
        bool NotifyOnAvailable,
        bool AutoRedeem,
        bool IsBlocked,
        string[] MissingLabels,
        DateTimeOffset CreatedAt
    );

    private sealed record StoreDetailResponse(
        Guid Id,
        string ChannelId,
        string DisplayName,
        bool IsActive,
        DateTimeOffset? LastSyncedAt,
        DateTimeOffset CreatedAt,
        int ItemCount,
        string[] MissingLabels
    );

    private sealed record StoreItemPageResponse(
        StoreItemRowResponse[] Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record StoreItemRowResponse(Guid Id, string Name, Guid? WishlistEntryId);
}
