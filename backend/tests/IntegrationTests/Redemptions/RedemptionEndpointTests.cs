using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;

namespace IntegrationTests.Redemptions;

/// <summary>
/// The redemption list's query string over the real pipeline.
///
/// A handler unit test cannot answer the question these ask: whether a malformed value is refused
/// by <c>SearchQuery</c> — with the <c>{code, params}</c> body the frontend catalog renders — or by
/// ASP.NET's model binder, which answers a bare ProblemDetails carrying neither. That distinction
/// only exists once a request has actually been bound, so it cannot be reached by calling the
/// handler directly; a strongly-typed parameter cannot even be handed the malformed value.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class RedemptionEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public RedemptionEndpointTests(DatabaseFixture fixture)
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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task A_malformed_wishlist_entry_id_is_rejected_with_the_documented_code()
    {
        var response = await GetAsync("/api/redemptions?wishlistEntryId=abc");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        // The specific code rides in the per-field entry, so one frontend branch resolves a bad
        // filter and a bad request body alike.
        var fieldError = problem.GetProperty("params").GetProperty("errors")[0];
        fieldError
            .GetProperty("code")
            .GetString()
            .Should()
            .Be(ErrorCodes.Search.InvalidFilterValue);
        fieldError.GetProperty("field").GetString().Should().Be("wishlistEntryId");
        fieldError
            .GetProperty("params")
            .GetProperty("parameter")
            .GetString()
            .Should()
            .Be("wishlistEntryId");
    }

    [Fact]
    public async Task A_well_formed_wishlist_entry_id_is_accepted()
    {
        var response = await GetAsync($"/api/redemptions?wishlistEntryId={Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
}
