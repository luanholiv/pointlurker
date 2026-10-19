using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;

namespace IntegrationTests.ErrorHandling;

/// <summary>
/// Drives every custom write endpoint against the real pipeline, which is the only thing that can
/// show per-field validation is *reachable*. The mapper this replaces was correct and wired to an
/// MVC hook that cannot fire in a minimal-API application, so it never ran — a defect no unit test
/// could see, because reachability is a question about the call graph rather than about names.
/// </summary>
/// <remarks>
/// None of these cases need seeded data. Binding failures happen before routing reaches a handler,
/// and the validation filter short-circuits before the handler looks anything up, so a
/// syntactically valid but arbitrary id is enough — and asserting that is itself part of the
/// contract.
/// </remarks>
[Collection(ContainersCollection.Name)]
public sealed class RequestValidationSweepTests : IAsyncLifetime, IDisposable
{
    private const string AnyId = "11111111-1111-1111-1111-111111111111";

    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public RequestValidationSweepTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _client = _factory.CreateClient();
        await RegisterAndLoginAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>Every custom endpoint that binds a body: verb, then path.</summary>
    public static TheoryData<string, string> BodyBoundEndpoints() =>
        new()
        {
            { "POST", "/api/stores" },
            { "PUT", $"/api/stores/{AnyId}/profile/values" },
            { "POST", $"/api/stores/{AnyId}/profile/aliases" },
            { "POST", "/api/wishlist" },
            { "PUT", $"/api/wishlist/{AnyId}" },
            { "PATCH", "/api/users/me/locale" },
            { "PUT", "/api/users/me/credentials/streamelements" },
        };

    /// <summary>Verb, path, a parseable body that breaks a rule, and the code it must report.</summary>
    public static TheoryData<string, string, string, string> InvalidBodies() =>
        new()
        {
            {
                "POST",
                "/api/stores",
                """{"channelId":"","displayName":""}""",
                ErrorCodes.Validation.Required
            },
            {
                "PUT",
                $"/api/stores/{AnyId}/profile/values",
                """{"values":[{"labelCanonical":"","value":"v"}]}""",
                ErrorCodes.Validation.Required
            },
            {
                "POST",
                $"/api/stores/{AnyId}/profile/aliases",
                """{"rawLabel":"","labelCanonical":""}""",
                ErrorCodes.Validation.Required
            },
            {
                "POST",
                "/api/wishlist",
                """{"storeItemId":"00000000-0000-0000-0000-000000000000","notifyOnAvailable":true,"autoRedeem":false}""",
                ErrorCodes.Validation.Required
            },
            {
                "PATCH",
                "/api/users/me/locale",
                """{"locale":""}""",
                ErrorCodes.Validation.Required
            },
            // A rule may name a code from any namespace when a more specific sentence exists.
            {
                "PUT",
                "/api/users/me/credentials/streamelements",
                """{"secret":"  "}""",
                ErrorCodes.Credentials.SecretRequired
            },
        };

    /// <summary>Every endpoint that reads the query string: verb, then path.</summary>
    public static TheoryData<string, string> QueryBoundEndpoints() =>
        new()
        {
            { "GET", "/api/stores" },
            { "GET", $"/api/stores/{AnyId}/items" },
            { "GET", "/api/wishlist" },
            { "GET", "/api/redemptions" },
            { "GET", "/api/stores/lookup" },
        };

    /// <summary>
    /// Verb, a path whose query string breaks a rule, the parameter that must be named, and the code
    /// it must report. Two producers are represented: <c>SearchQuery</c>, which parses the list
    /// endpoints' filters, and a FluentValidation rule on an <c>[AsParameters]</c> record.
    /// </summary>
    public static TheoryData<string, string, string, string> InvalidQueries() =>
        new()
        {
            { "GET", "/api/stores?active=maybe", "active", ErrorCodes.Search.InvalidFilterValue },
            {
                "GET",
                $"/api/stores/{AnyId}/items?sort=bogus",
                "sort",
                ErrorCodes.Search.InvalidEnumValue
            },
            { "GET", "/api/wishlist?store=abc", "store", ErrorCodes.Search.InvalidFilterValue },
            { "GET", "/api/redemptions?status=nope", "status", ErrorCodes.Search.InvalidEnumValue },
            {
                "GET",
                "/api/stores/lookup?provider=streamelements",
                "query",
                ErrorCodes.StoreLookup.QueryRequired
            },
            {
                "GET",
                "/api/stores/lookup?provider=bogus",
                "provider",
                ErrorCodes.Stores.ProviderUnsupported
            },
        };

    [Theory]
    [MemberData(nameof(BodyBoundEndpoints))]
    public async Task Unparseable_body_answers_malformed_body(string method, string path)
    {
        var response = await SendAsync(method, path, """{"truncated":""", authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Request.MalformedBody);
        body.GetProperty("params").EnumerateObject().Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(InvalidBodies))]
    public async Task Invalid_body_answers_validation_failed_with_the_rule_that_broke(
        string method,
        string path,
        string requestBody,
        string expectedFieldCode
    )
    {
        var response = await SendAsync(method, path, requestBody, authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        var errors = body.GetProperty("params").GetProperty("errors");
        errors.GetArrayLength().Should().BeGreaterThan(0);

        var first = errors[0];
        first.GetProperty("code").GetString().Should().Be(expectedFieldCode);
        first.GetProperty("field").GetString().Should().NotBeNullOrWhiteSpace();
        // The backend answers in codes; the sentence is the frontend's to render.
        first.TryGetProperty("message", out _).Should().BeFalse();
    }

    /// <summary>
    /// Validation must not run ahead of authorization, or an anonymous caller could probe which
    /// bodies a protected endpoint accepts.
    /// </summary>
    [Theory]
    [MemberData(nameof(BodyBoundEndpoints))]
    public async Task Invalid_body_without_credentials_answers_401(string method, string path)
    {
        var response = await SendAsync(method, path, "{}", authenticated: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task A_body_less_endpoint_ignores_a_body_entirely(string action)
    {
        var response = await SendAsync(
            "PATCH",
            $"/api/stores/{AnyId}/{action}",
            """{"truncated":""",
            authenticated: true
        );

        // Nothing reads the body, so the unparseable JSON is never seen and the handler answers on
        // the merits: no such store.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Stores.NotFound);
    }

    /// <summary>
    /// The query surface answers in the same envelope as a body, naming the query-string parameter
    /// as the field. Keeping both sweeps in one file is what makes this the single place that
    /// answers "what does this endpoint do with bad input".
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Invalid_query_answers_validation_failed_with_the_parameter_that_broke(
        string method,
        string path,
        string expectedField,
        string expectedFieldCode
    )
    {
        var response = await SendAsync(method, path, authenticated: true);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var first = await FirstFieldErrorAsync(response);
        first.GetProperty("code").GetString().Should().Be(expectedFieldCode);
        first.GetProperty("field").GetString().Should().Be(expectedField);
        // The backend answers in codes; the sentence is the frontend's to render.
        first.TryGetProperty("message", out _).Should().BeFalse();
    }

    /// <summary>
    /// A rejected filter is refused before the store is looked up, which is why none of these cases
    /// need seeded data — and is itself part of the contract: a typo must not cost a query.
    /// </summary>
    [Fact]
    public async Task Invalid_query_is_refused_before_the_row_is_looked_up()
    {
        var response = await SendAsync(
            "GET",
            $"/api/stores/{AnyId}/items?sort=bogus",
            authenticated: true
        );

        // AnyId names no store, so reaching the handler would have answered 404 instead.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Every bad parameter is reported at once. The whole-request shape this replaced could only
    /// name the first, so a caller with two typos paid for two round trips.
    /// </summary>
    [Fact]
    public async Task Invalid_query_reports_every_parameter_that_broke()
    {
        var response = await SendAsync(
            "GET",
            $"/api/stores/{AnyId}/items?sort=bogus&min=abc",
            authenticated: true
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        var fields = body.GetProperty("params")
            .GetProperty("errors")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("field").GetString())
            .ToList();

        fields.Should().BeEquivalentTo(["min", "sort"]);
    }

    /// <summary>
    /// Authorization precedes query validation for the same reason it precedes body validation: an
    /// anonymous caller must not be able to probe which filters a protected endpoint accepts.
    /// </summary>
    [Theory]
    [MemberData(nameof(QueryBoundEndpoints))]
    public async Task Invalid_query_without_credentials_answers_401(string method, string path)
    {
        var response = await SendAsync(method, $"{path}?sort=bogus", authenticated: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The two caps that moved out of the application layer, at the boundary that now owns them.
    /// </summary>
    [Fact]
    public async Task An_over_length_redemption_message_reports_the_cap_per_field()
    {
        var tooLong = new string('x', WishlistEntry.RedemptionMessageMaxLength + 1);
        var requestBody = JsonSerializer.Serialize(
            new
            {
                notifyOnAvailable = true,
                autoRedeem = false,
                redemptionMessage = tooLong,
            }
        );

        var response = await SendAsync(
            "PUT",
            $"/api/wishlist/{AnyId}",
            requestBody,
            authenticated: true
        );

        var first = await FirstFieldErrorAsync(response);
        first.GetProperty("field").GetString().Should().Be("redemptionMessage");
        first.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.TooLong);
        first
            .GetProperty("params")
            .GetProperty("max")
            .GetInt32()
            .Should()
            .Be(WishlistEntry.RedemptionMessageMaxLength);
    }

    [Fact]
    public async Task An_over_length_profile_value_reports_the_cap_and_the_failing_element()
    {
        var tooLong = new string('x', UpsertStoreProfileValues.MaxValueLength + 1);
        var requestBody = JsonSerializer.Serialize(
            new { values = new[] { new { labelCanonical = "name", value = tooLong } } }
        );

        var response = await SendAsync(
            "PUT",
            $"/api/stores/{AnyId}/profile/values",
            requestBody,
            authenticated: true
        );

        var first = await FirstFieldErrorAsync(response);
        first.GetProperty("field").GetString().Should().Be("values[0].value");
        first.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.TooLong);
        first
            .GetProperty("params")
            .GetProperty("max")
            .GetInt32()
            .Should()
            .Be(UpsertStoreProfileValues.MaxValueLength);
    }

    /// <summary>
    /// A rejected value must never travel back to the client: one of these contracts carries the
    /// StreamElements token, and the whole response is also what lands in a log.
    /// </summary>
    [Fact]
    public async Task A_rejected_value_is_never_echoed_back()
    {
        const string secret = "eyJhbGciOiJIUzI1NiJ9.not-a-real-token.signature";
        var tooLong = new string('x', UpsertStoreProfileValues.MaxValueLength + 1);
        var requestBody = JsonSerializer.Serialize(
            new { values = new[] { new { labelCanonical = secret, value = tooLong } } }
        );

        var response = await SendAsync(
            "PUT",
            $"/api/stores/{AnyId}/profile/values",
            requestBody,
            authenticated: true
        );

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(secret);
        raw.Should().NotContain(tooLong);
    }

    private async Task<JsonElement> FirstFieldErrorAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await ParseProblemDetailsAsync(response);
        body.GetProperty("code").GetString().Should().Be(ErrorCodes.Validation.Failed);

        return body.GetProperty("params").GetProperty("errors")[0];
    }

    private Task<HttpResponseMessage> SendAsync(
        string method,
        string path,
        string requestBody,
        bool authenticated
    ) =>
        SendAsync(
            method,
            path,
            authenticated,
            new StringContent(requestBody, Encoding.UTF8, "application/json")
        );

    /// <summary>
    /// The query surface sends no body at all — attaching an empty one would be a different request
    /// from the one a browser makes.
    /// </summary>
    private Task<HttpResponseMessage> SendAsync(string method, string path, bool authenticated) =>
        SendAsync(method, path, authenticated, content: null);

    private Task<HttpResponseMessage> SendAsync(
        string method,
        string path,
        bool authenticated,
        HttpContent? content
    )
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = content };

        if (authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return _client.SendAsync(request);
    }

    private async Task RegisterAndLoginAsync()
    {
        var credentials = new { email = "test@example.com", password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", credentials);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", credentials);
        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();

        _accessToken = login.GetProperty("accessToken").GetString()!;
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
