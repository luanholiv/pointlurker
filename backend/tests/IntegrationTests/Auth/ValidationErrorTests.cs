using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace IntegrationTests.Auth;

// The framework Identity API auth endpoints answer failures under the RFC 7807 {code, params}
// contract like every other endpoint. Their built-in ValidationProblem shape is kept alongside it
// rather than replaced, so `errors` remains readable in a log while `code`/`params` are what a
// client renders. These tests pin both halves of that body.
[Collection(ContainersCollection.Name)]
public sealed class ValidationErrorTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public ValidationErrorTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task Register_with_weak_password_returns_identity_validation_shape()
    {
        // "abc" is too short and missing digit, upper, special.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "newuser@example.com", password = "abc" }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await ParseProblemDetailsAsync(response);

        // Built-in Identity shape: an `errors` object keyed by Identity error codes.
        body.TryGetProperty("errors", out var errors)
            .Should()
            .BeTrue("Identity uses an errors map");
        errors.EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Register_failure_carries_our_code_params_contract()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "newuser@example.com", password = "abc" }
        );

        var body = await ParseProblemDetailsAsync(response);

        // The {code, params} extensions reach the framework auth endpoints too, so nothing the
        // client renders has to come from the response prose.
        body.TryGetProperty("code", out var code).Should().BeTrue();
        code.GetString().Should().Be("validation.failed");
        body.TryGetProperty("params", out var problemParams).Should().BeTrue();
        problemParams.GetProperty("errors").EnumerateArray().Should().NotBeEmpty();
    }

    private static async Task<JsonElement> ParseProblemDetailsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
    }
}
