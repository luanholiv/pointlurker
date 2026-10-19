using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.BearerToken;

namespace IntegrationTests.Auth;

[Collection(ContainersCollection.Name)]
public sealed class AccountManagementTests : IAsyncLifetime, IDisposable
{
    private const string Password = "Password123!";
    private const string NewPassword = "Password456!";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _email = null!;

    public AccountManagementTests(DatabaseFixture fixture)
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
    public async Task Me_endpoint_returns_current_user_without_roles()
    {
        await RegisterAsync();
        var token = await LoginAsync();

        var response = await AuthedGetAsync("/api/users/me", token);

        Assert.True(response.IsSuccessStatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(payload);
        Assert.Equal(_email, payload!.Email);

        // The /me payload intentionally carries no roles field.
        var raw = await AuthedGetAsync("/api/users/me", token);
        var rawBody = await raw.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"roles\"", rawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Change_password_via_manage_info_requires_new_password_for_login()
    {
        await RegisterAsync();
        var token = await LoginAsync();

        // Password change is now the framework endpoint POST /api/auth/manage/info.
        var changeResponse = await AuthedPostAsJsonAsync(
            "/api/auth/manage/info",
            new { newPassword = NewPassword, oldPassword = Password },
            token
        );

        Assert.True(
            changeResponse.IsSuccessStatusCode,
            $"Unexpected status {changeResponse.StatusCode}: {await changeResponse.Content.ReadAsStringAsync()}"
        );

        var oldLogin = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = NewPassword }
        );
        Assert.True(newLogin.IsSuccessStatusCode);
        var tokenResponse = await newLogin.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(tokenResponse);
        Assert.False(string.IsNullOrWhiteSpace(tokenResponse!.AccessToken));
    }

    private async Task RegisterAsync()
    {
        var registerResponse = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = _email, password = Password }
        );

        Assert.True(registerResponse.IsSuccessStatusCode);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );

        Assert.True(response.IsSuccessStatusCode);
        var tokenResponse = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(tokenResponse);
        return tokenResponse!.AccessToken;
    }

    private Task<HttpResponseMessage> AuthedPostAsJsonAsync<T>(string uri, T payload, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.PostAsJsonAsync(uri, payload);
    }

    private Task<HttpResponseMessage> AuthedGetAsync(string uri, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync(uri);
    }

    private sealed record CurrentUserResponse(Guid UserId, string Email);
}
