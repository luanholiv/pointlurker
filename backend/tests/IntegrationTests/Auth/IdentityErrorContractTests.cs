using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace IntegrationTests.Auth;

/// <summary>
/// The framework Identity endpoints now answer failures under the same <c>{code, params}</c>
/// contract as every other endpoint, so a client can render its own localized copy instead of the
/// English prose the server picked. These tests drive the real endpoints rather than the middleware,
/// which is what pins the mapping to the codes ASP.NET Identity actually emits — those strings come
/// from the framework, not from us, and a version bump can change them.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class IdentityErrorContractTests : IAsyncLifetime, IDisposable
{
    private const string Password = "Password123!";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _email = null!;

    public IdentityErrorContractTests(DatabaseFixture fixture) => _fixture = fixture;

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
    public async Task Weak_password_on_register_carries_the_password_rule_codes()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = _email, password = "abc" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ReadBodyAsync(response);
        body.GetProperty("code").GetString().Should().Be("validation.failed");

        var codes = body.GetProperty("params")
            .GetProperty("errors")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("code").GetString())
            .ToArray();

        codes.Should().Contain("validation.password_too_short");
    }

    [Fact]
    public async Task Password_length_rule_carries_the_configured_minimum_as_a_number()
    {
        // The number lives only in Identity's English sentence, so this is the assertion that the
        // frontend's "{{min}}" placeholder has something real to interpolate.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = _email, password = "abc" }
        );

        var body = await ReadBodyAsync(response);
        var tooShort = body.GetProperty("params")
            .GetProperty("errors")
            .EnumerateArray()
            .First(entry =>
                entry.GetProperty("code").GetString() == "validation.password_too_short"
            );

        var min = tooShort.GetProperty("params").GetProperty("min");
        min.ValueKind.Should().Be(JsonValueKind.Number);
        min.GetInt32().Should().BePositive();
    }

    [Fact]
    public async Task Second_registration_reports_the_single_user_invariant()
    {
        var first = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = _email, password = Password }
        );
        first.IsSuccessStatusCode.Should().BeTrue();

        var second = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = $"other-{Guid.NewGuid():N}@example.com", password = Password }
        );

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadBodyAsync(second);
        var codes = body.GetProperty("params")
            .GetProperty("errors")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("code").GetString())
            .ToArray();

        codes.Should().Contain("users.register_already_exists");
    }

    [Fact]
    public async Task Wrong_credentials_report_a_single_unauthorized_code()
    {
        await RegisterAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = "WrongPassword123!" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await ReadBodyAsync(response);
        body.GetProperty("code").GetString().Should().Be("auth.unauthorized");
    }

    [Fact]
    public async Task Wrong_current_password_on_change_reports_the_mismatch_code()
    {
        await RegisterAsync();
        var token = await LoginAsync();

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync(
            "/api/auth/manage/info",
            new { newPassword = "Password456!", oldPassword = "NotTheCurrentOne1!" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadBodyAsync(response);
        body.GetProperty("code").GetString().Should().Be("validation.failed");

        var codes = body.GetProperty("params")
            .GetProperty("errors")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("code").GetString())
            .ToArray();

        codes.Should().Contain("validation.password_mismatch");
    }

    [Fact]
    public async Task Successful_login_still_returns_the_token_pair()
    {
        await RegisterAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await ReadBodyAsync(response);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("code", out _).Should().BeFalse();
    }

    [Fact]
    public async Task A_refresh_with_a_bad_token_stays_a_bodyless_challenge()
    {
        // Identity answers a dead refresh token with a bare challenge, not a problem document, so
        // there is nothing to add a code to — and nothing to render either: the client's only move
        // is to drop the session. The middleware must leave it exactly as it is.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = "not-a-token" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    private async Task RegisterAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = _email, password = Password }
        );

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = _email, password = Password }
        );

        response.IsSuccessStatusCode.Should().BeTrue();
        var body = await ReadBodyAsync(response);
        return body.GetProperty("accessToken").GetString()!;
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
