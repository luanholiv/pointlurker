using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PointLurker.Core.Domain.Errors;

namespace IntegrationTests.ErrorHandling;

// The RFC 7807 {code, params} contract is asserted against a CUSTOM (non-auth) endpoint, since the
// framework Identity API auth endpoints are exempt from it. The locale endpoint returns a domain
// error in the contract shape.
[Collection(ContainersCollection.Name)]
public sealed class ProblemDetailsShapeTests : IAsyncLifetime, IDisposable
{
    private const string UnsupportedLocale = "xx-XX";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public ProblemDetailsShapeTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        _accessToken = await RegisterAndLoginAsync("test@example.com", "Password123!");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task Failing_endpoint_returns_application_problem_json_content_type()
    {
        var response = await PatchLocaleAsync(UnsupportedLocale);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Failing_endpoint_response_includes_code_field()
    {
        var response = await PatchLocaleAsync(UnsupportedLocale);

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Users.LocaleUnsupported);
    }

    [Fact]
    public async Task AcceptLanguage_pt_BR_does_not_localize_error_title()
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me/locale");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("Accept-Language", "pt-BR");
        request.Content = JsonContent.Create(new { locale = UnsupportedLocale });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ParseProblemDetailsAsync(response);
        var title = body.GetProperty("title").GetString();
        // Title must be English regardless of Accept-Language.
        title.Should().Be($"Locale '{UnsupportedLocale}' is not supported.");
    }

    [Fact]
    public async Task Error_code_matches_bounded_context_namespace_pattern()
    {
        var response = await PatchLocaleAsync(UnsupportedLocale);

        var body = await ParseProblemDetailsAsync(response);
        var code = body.GetProperty("code").GetString();
        code.Should().MatchRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$");
    }

    [Fact]
    public async Task Malformed_body_on_a_custom_endpoint_returns_400_with_malformed_body_code()
    {
        var response = await PatchLocaleWithRawBodyAsync("{\"locale\":");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Request.MalformedBody);
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("params").EnumerateObject().Should().BeEmpty();
    }

    [Fact]
    public async Task Login_body_missing_required_properties_returns_400_not_500()
    {
        // The framework Identity endpoints are exempt from the {code, params} contract, but the
        // global exception handler still owns the BadHttpRequestException they raise: a caller's
        // bad body must not be reported as a server fault.
        var response = await _client.PostAsync(
            "/api/auth/login",
            new StringContent("{}", Encoding.UTF8, "application/json")
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Request.MalformedBody);
    }

    private Task<HttpResponseMessage> PatchLocaleWithRawBodyAsync(string rawJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me/locale")
        {
            Content = new StringContent(rawJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PatchLocaleAsync(string locale)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/users/me/locale")
        {
            Content = JsonContent.Create(new { locale }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return _client.SendAsync(request);
    }

    private async Task<string> RegisterAndLoginAsync(string email, string password)
    {
        await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password }
        );
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return loginBody!.AccessToken;
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
}
