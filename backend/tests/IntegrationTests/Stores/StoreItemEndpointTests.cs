using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Stores;

/// <summary>
/// The store-items endpoint over the real pipeline. Handler unit tests cannot answer two of the
/// questions that matter most here: whether the route is actually behind authorization, and whether
/// ASP.NET binds the frontend's existing query-string names to the handler at all.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class StoreItemEndpointTests : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;
    private Guid _storeId;

    public StoreItemEndpointTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString)
        {
            MockStreamElementsApi = Substitute.For<IStoreProviderClient>(),
        };
        _client = _factory.CreateClient();
        await RegisterAndLoginAsync();
        await SeedStoreAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task An_anonymous_caller_is_rejected()
    {
        var response = await _client.GetAsync($"/api/stores/{_storeId}/items");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_anonymous_caller_is_rejected_from_the_facets_endpoint()
    {
        var response = await _client.GetAsync($"/api/stores/{_storeId}/items/facets");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_unknown_store_is_reported_as_not_found()
    {
        var response = await GetAsync($"/api/stores/{Guid.NewGuid()}/items");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Items_are_returned_for_an_existing_store()
    {
        var page = await GetPageAsync($"/api/stores/{_storeId}/items?avail=0");

        page.Items.Should().NotBeEmpty();
        page.TotalCount.Should().Be(4);
        page.Offset.Should().Be(0);
    }

    /// <summary>
    /// The whole point of reusing the browser's parameter names: the frontend's existing serializer
    /// becomes the API query string with no translation layer, so a deep link keeps resolving.
    /// </summary>
    [Fact]
    public async Task A_url_from_the_frontends_serializer_is_understood_verbatim()
    {
        var page = await GetPageAsync(
            $"/api/stores/{_storeId}/items?q=e&avail=0&sub=0&feat=0&cat=Emotes,Badges&min=1&max=1000&sort=cost&dir=desc"
        );

        page.Items.Select(item => item.Cost).Should().BeInDescendingOrder();
        page.Items.Should()
            .OnlyContain(item => item.CategoryName == "Emotes" || item.CategoryName == "Badges");
    }

    [Fact]
    public async Task An_unknown_sort_is_rejected_with_the_documented_code()
    {
        var response = await GetAsync($"/api/stores/{_storeId}/items?sort=bogus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        var fieldError = problem.GetProperty("params").GetProperty("errors")[0];
        fieldError.GetProperty("code").GetString().Should().Be(ErrorCodes.Search.InvalidEnumValue);
        fieldError.GetProperty("field").GetString().Should().Be("sort");
        fieldError.GetProperty("params").GetProperty("parameter").GetString().Should().Be("sort");
    }

    /// <summary>
    /// Two bad filters, one answer. The whole-request shape could only ever name the first.
    /// </summary>
    [Fact]
    public async Task Every_unreadable_filter_is_named_in_one_answer()
    {
        var response = await GetAsync($"/api/stores/{_storeId}/items?sort=bogus&min=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fields = problem
            .GetProperty("params")
            .GetProperty("errors")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("field").GetString())
            .ToList();

        fields.Should().BeEquivalentTo(["min", "sort"]);
    }

    [Fact]
    public async Task Availability_defaults_to_on_over_the_wire()
    {
        var withDefault = await GetPageAsync($"/api/stores/{_storeId}/items");
        var withoutFilter = await GetPageAsync($"/api/stores/{_storeId}/items?avail=0");

        withDefault.TotalCount.Should().BeLessThan(withoutFilter.TotalCount);
        withDefault.Items.Should().OnlyContain(item => item.IsAvailable);
    }

    [Fact]
    public async Task The_page_carries_only_the_shared_envelope()
    {
        var response = await GetAsync($"/api/stores/{_storeId}/items");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.EnumerateObject()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo("items", "totalCount", "offset", "limit");
    }

    [Fact]
    public async Task Facets_report_the_whole_store_regardless_of_any_filter()
    {
        var response = await GetAsync($"/api/stores/{_storeId}/items/facets?cat=Emotes&min=500");
        var facets = await response.Content.ReadFromJsonAsync<JsonElement>();

        // "Apparel" is the one that matters: the request asked for Emotes only, and the category
        // list still offers everything the store holds. Narrowing it here is what would make the
        // dropdown shrink as the user picks from it.
        facets
            .GetProperty("categories")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should()
            .BeEquivalentTo("Apparel", "Badges", "Emotes");
        facets.GetProperty("minCost").GetInt64().Should().Be(50);
        facets.GetProperty("maxCost").GetInt64().Should().Be(900);
    }

    [Fact]
    public async Task The_store_envelope_no_longer_carries_items_but_still_counts_them()
    {
        var response = await GetAsync($"/api/stores/{_storeId}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        body.TryGetProperty("items", out _).Should().BeFalse();
        body.GetProperty("itemCount").GetInt32().Should().Be(4);
    }

    /// <summary>
    /// The two empty states read differently to a user — "this store has no items" versus "no items
    /// match your filters" — so the envelope's count has to survive a filter that matches nothing.
    /// </summary>
    [Fact]
    public async Task An_empty_store_is_distinguishable_from_an_over_filtered_one()
    {
        var page = await GetPageAsync($"/api/stores/{_storeId}/items?q=nothingmatchesthis");
        var envelope = await GetAsync($"/api/stores/{_storeId}");
        var body = await envelope.Content.ReadFromJsonAsync<JsonElement>();

        page.TotalCount.Should().Be(0);
        body.GetProperty("itemCount").GetInt32().Should().Be(4);
    }

    private async Task<StoreItemPage> GetPageAsync(string url)
    {
        var response = await GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<StoreItemPage>(Json))!;
    }

    private async Task<HttpResponseMessage> GetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private async Task RegisterAndLoginAsync()
    {
        var credentials = new { email = "test@example.com", password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", credentials);
        var login = await _client.PostAsJsonAsync("/api/auth/login", credentials);
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        _accessToken = body.GetProperty("accessToken").GetString()!;
    }

    private async Task SeedStoreAsync()
    {
        _storeId = Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<AppDbContext>();

        var store = new WatchedStore(
            _storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-items-endpoint"),
            "Items",
            Guid.NewGuid(),
            Now
        );
        store.AddItem(Item(1, "Emote One", 100, "Emotes", current: 5));
        store.AddItem(Item(2, "Emote Two", 200, "Emotes", current: 0));
        store.AddItem(Item(3, "Badge", 50, "Badges", current: -1));
        store.AddItem(Item(4, "Cape", 900, "Apparel", current: 3));

        db.WatchedStores.Add(store);
        await db.SaveChangesAsync();
    }

    private StoreItem Item(int index, string name, long cost, string category, int current) =>
        new(
            Guid.NewGuid(),
            _storeId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId($"se-endpoint-{index}"),
                new StoreExternalId("ch-items-endpoint"),
                name,
                null,
                "perk",
                cost,
                category,
                true,
                false,
                true,
                false,
                false,
                [],
                [],
                new ItemQuantity(10, current),
                new ItemCooldown(0, 0, 0),
                Now.AddMinutes(index),
                null,
                Now
            )
        );

    private sealed record StoreItemPage(
        IReadOnlyList<StoreItemRow> Items,
        int TotalCount,
        int Offset,
        int Limit
    );

    private sealed record StoreItemRow(
        Guid Id,
        string Name,
        long Cost,
        string CategoryName,
        bool IsAvailable,
        Guid? WishlistEntryId,
        DateTimeOffset ProviderCreatedAt
    );
}
