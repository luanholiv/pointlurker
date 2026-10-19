using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace IntegrationTests.Auth;

// Core framework Identity API flow: register -> login -> refresh, plus refresh rejection and
// security-stamp revocation on password change.
[Collection(ContainersCollection.Name)]
public sealed class IdentityApiAuthTests : IAsyncLifetime, IDisposable
{
    private const string Password = "Password123!";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _email = null!;

    public IdentityApiAuthTests(DatabaseFixture fixture)
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

    [Fact]
    public async Task Login_returns_a_refreshable_token_pair()
    {
        await RegisterAsync();

        var tokens = await LoginAsync();

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.True(tokens.ExpiresIn > 0);
    }

    [Fact]
    public async Task Refresh_exchanges_a_valid_refresh_token_and_new_access_token_authenticates()
    {
        await RegisterAsync();
        var tokens = await LoginAsync();

        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = tokens.RefreshToken }
        );
        Assert.True(
            refreshResponse.IsSuccessStatusCode,
            $"refresh failed: {refreshResponse.StatusCode} {await refreshResponse.Content.ReadAsStringAsync()}"
        );
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.False(string.IsNullOrWhiteSpace(refreshed!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.RefreshToken));

        // The new access token authenticates a protected request.
        var me = await AuthedGetAsync("/api/users/me", refreshed.AccessToken);
        Assert.True(me.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Refresh_rejects_a_garbage_token()
    {
        await RegisterAsync();
        await LoginAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = "not-a-real-refresh-token" }
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Password_change_revokes_outstanding_refresh_tokens()
    {
        await RegisterAsync();
        var tokens = await LoginAsync();

        // Change the password (security-stamp rotates).
        var change = await AuthedPostAsync(
            "/api/auth/manage/info",
            new { newPassword = "Password456!", oldPassword = Password },
            tokens.AccessToken
        );
        Assert.True(
            change.IsSuccessStatusCode,
            $"password change failed: {change.StatusCode} {await change.Content.ReadAsStringAsync()}"
        );

        // The pre-change refresh token is now rejected.
        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = tokens.RefreshToken }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    private Task RegisterAsync() =>
        _client.PostAsJsonAsync("/api/auth/register", new { email = _email, password = Password });

    private async Task<AccessTokenResponse> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );
        Assert.True(response.IsSuccessStatusCode);
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
    }

    private Task<HttpResponseMessage> AuthedGetAsync(string uri, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync(uri);
    }

    private Task<HttpResponseMessage> AuthedPostAsync<T>(string uri, T payload, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsJsonAsync(uri, payload);
    }
}
