using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.ValueObjects;

namespace IntegrationTests.Stores;

[Collection(ContainersCollection.Name)]
public sealed class StoreLookupEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private readonly IStoreProviderClient _mockSeApi = Substitute.For<IStoreProviderClient>();
    private readonly IStoreProviderClient _mockGauPoints = Substitute.For<IStoreProviderClient>();
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public StoreLookupEndpointTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task A_streamelements_lookup_returns_the_channel()
    {
        _mockSeApi
            .LookupStoreAsync("xqc", Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("channel-id-123"),
                    "xqc",
                    "xQc",
                    "https://cdn.example.com/xqc.png"
                )
            );

        var response = await AuthenticatedGetAsync(
            "/api/stores/lookup?provider=streamelements&query=xqc"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LookupBody>();
        body!.ExternalId.Should().Be("channel-id-123");
        body.Handle.Should().Be("xqc");
        body.DisplayName.Should().Be("xQc");
        body.AvatarUrl.Should().Be("https://cdn.example.com/xqc.png");
        body.ItemCount.Should().BeNull();
    }

    [Fact]
    public async Task A_gaupoints_lookup_needs_no_query_and_reports_the_item_count()
    {
        _mockGauPoints
            .LookupStoreAsync(string.Empty, Arg.Any<CancellationToken>())
            .Returns(
                new ProviderStoreLookup(
                    new StoreExternalId("gaupoints"),
                    "gaupoints",
                    "GauPoints",
                    "https://gaupoints.com.br/logo.png",
                    ItemCount: 12
                )
            );

        var response = await AuthenticatedGetAsync("/api/stores/lookup?provider=gaupoints");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LookupBody>();
        body!.ExternalId.Should().Be("gaupoints");
        body.ItemCount.Should().Be(12);
        await _mockSeApi
            .DidNotReceive()
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_channel_answers_404_naming_provider_and_query()
    {
        _mockSeApi
            .LookupStoreAsync("nobody", Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(404, "Not Found", "Not found"));

        var response = await AuthenticatedGetAsync(
            "/api/stores/lookup?provider=streamelements&query=nobody"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.StoreLookup.NotFound);
        body.GetProperty("params")
            .GetProperty("provider")
            .GetString()
            .Should()
            .Be("streamelements");
        body.GetProperty("params").GetProperty("query").GetString().Should().Be("nobody");
    }

    [Theory]
    [InlineData("")]
    [InlineData("&query=")]
    [InlineData("&query=%20%20")]
    public async Task A_streamelements_lookup_without_a_query_is_refused_per_field(string query)
    {
        var response = await AuthenticatedGetAsync(
            $"/api/stores/lookup?provider=streamelements{query}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);
        var fieldError = body.GetProperty("params").GetProperty("errors")[0];
        fieldError
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.StoreLookup.QueryRequired);
        fieldError.GetProperty("field").GetString().Should().Be("query");

        await _mockSeApi
            .DidNotReceive()
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/api/stores/lookup?query=xqc")]
    [InlineData("/api/stores/lookup?provider=twitch&query=xqc")]
    [InlineData("/api/stores/lookup?provider=0&query=xqc")]
    public async Task A_provider_that_does_not_parse_is_refused_per_field(string url)
    {
        var response = await AuthenticatedGetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var fieldError = (await ParseProblemDetailsAsync(response))
            .GetProperty("params")
            .GetProperty("errors")[0];
        fieldError
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.Stores.ProviderUnsupported);
        fieldError.GetProperty("field").GetString().Should().Be("provider");
    }

    [Fact]
    public async Task An_unreachable_provider_answers_502_naming_it()
    {
        _mockGauPoints
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(
                new ProviderApiException(
                    ProviderApiException.NoResponse,
                    null,
                    "Connection refused"
                )
            );

        var response = await AuthenticatedGetAsync("/api/stores/lookup?provider=gaupoints");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.ProviderApi.Unreachable);
        body.GetProperty("params").GetProperty("provider").GetString().Should().Be("gaupoints");
    }

    [Fact]
    public async Task A_provider_error_answers_502_fetch_failed()
    {
        _mockGauPoints
            .LookupStoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(500, "Server Error", "boom"));

        var response = await AuthenticatedGetAsync("/api/stores/lookup?provider=gaupoints");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.ProviderApi.FetchFailed);
    }

    [Fact]
    public async Task The_lookup_requires_authentication()
    {
        var response = await _client.GetAsync("/api/stores/lookup?provider=gaupoints");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private async Task<HttpResponseMessage> AuthenticatedGetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private static async Task<JsonElement> ParseProblemDetailsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
    }

    private sealed record LoginResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn
    );

    private sealed record LookupBody(
        string ExternalId,
        string Handle,
        string DisplayName,
        string? AvatarUrl,
        int? ItemCount
    );
}
