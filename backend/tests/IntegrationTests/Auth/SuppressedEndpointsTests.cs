using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace IntegrationTests.Auth;

// Endpoint suppression + authentication-based authorization + the /me path move.
[Collection(ContainersCollection.Name)]
public sealed class SuppressedEndpointsTests : IAsyncLifetime, IDisposable
{
    private const string Password = "Password123!";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _email = null!;

    public SuppressedEndpointsTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _email = $"admin-{Guid.NewGuid():N}@example.com";
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Theory]
    [InlineData("/api/auth/forgotPassword")]
    [InlineData("/api/auth/resetPassword")]
    [InlineData("/api/auth/resendConfirmationEmail")]
    public async Task Suppressed_post_endpoints_return_404(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { email = _email });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Suppressed_confirmEmail_get_returns_404()
    {
        var response = await _client.GetAsync("/api/auth/confirmEmail?userId=x&code=y");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_manage_info_is_suppressed_but_post_is_not()
    {
        await RegisterAsync();
        var token = await LoginAsync();

        var get = await AuthedSendAsync(HttpMethod.Get, "/api/auth/manage/info", token);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // POST manage/info (no changes) is processed normally.
        var post = await AuthedPostAsync("/api/auth/manage/info", new { }, token);
        Assert.NotEqual(HttpStatusCode.NotFound, post.StatusCode);
        Assert.True(
            post.IsSuccessStatusCode,
            $"POST manage/info should succeed: {post.StatusCode} {await post.Content.ReadAsStringAsync()}"
        );
    }

    [Fact]
    public async Task Protected_endpoint_requires_authentication_no_role()
    {
        await RegisterAsync();

        var unauth = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        var token = await LoginAsync();
        var authed = await AuthedSendAsync(HttpMethod.Get, "/api/users/me", token);
        Assert.True(authed.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Old_auth_paths_no_longer_resolve()
    {
        await RegisterAsync();
        var token = await LoginAsync();

        var oldMe = await AuthedSendAsync(HttpMethod.Get, "/api/auth/me", token);
        Assert.Equal(HttpStatusCode.NotFound, oldMe.StatusCode);

        var oldSeToken = await AuthedSendAsync(HttpMethod.Delete, "/api/auth/se-token", token);
        Assert.Equal(HttpStatusCode.NotFound, oldSeToken.StatusCode);
    }

    private Task RegisterAsync() =>
        _client.PostAsJsonAsync("/api/auth/register", new { email = _email, password = Password });

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );
        Assert.True(response.IsSuccessStatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        return tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> AuthedSendAsync(HttpMethod method, string uri, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(new HttpRequestMessage(method, uri));
    }

    private Task<HttpResponseMessage> AuthedPostAsync<T>(string uri, T payload, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsJsonAsync(uri, payload);
    }
}
