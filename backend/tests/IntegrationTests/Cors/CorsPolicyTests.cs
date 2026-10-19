using FluentAssertions;

namespace IntegrationTests.Cors;

// Guards the shipped default: no CORS headers unless `Cors:AllowedOrigins` is set. Development keeps
// a permissive policy so the cross-origin Vite dev server works with no configuration.
[Collection(ContainersCollection.Name)]
public sealed class CorsPolicyTests : IAsyncLifetime
{
    private const string AllowedOrigin = "https://app.example.com";
    private const string OtherOrigin = "https://evil.example.com";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";

    private readonly DatabaseFixture _fixture;

    public CorsPolicyTests(DatabaseFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Development_without_configured_origins_allows_any_origin()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await PreflightAsync(client, OtherOrigin);

        response.Headers.GetValues(AllowOriginHeader).Should().Equal("*");
    }

    [Fact]
    public async Task Configured_origins_restrict_even_in_development()
    {
        using var factory = CreateFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            }
        );
        using var client = factory.CreateClient();

        var allowed = await PreflightAsync(client, AllowedOrigin);
        var rejected = await PreflightAsync(client, OtherOrigin);

        allowed.Headers.GetValues(AllowOriginHeader).Should().Equal(AllowedOrigin);
        rejected.Headers.Contains(AllowOriginHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Configured_origins_are_echoed_on_a_simple_request()
    {
        using var factory = CreateFactory(
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            }
        );
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", AllowedOrigin);
        var response = await client.SendAsync(request);

        response.Headers.GetValues(AllowOriginHeader).Should().Equal(AllowedOrigin);
    }

    [Fact]
    public async Task Production_without_configured_origins_sends_no_cors_headers()
    {
        var keyRingPath = Path.Combine(Path.GetTempPath(), $"cors-tests-{Guid.NewGuid():N}");

        try
        {
            using var factory = CreateFactory(
                environment: "Production",
                extraConfiguration: new Dictionary<string, string?>
                {
                    ["DataProtection:KeyRingPath"] = keyRingPath,
                }
            );
            using var client = factory.CreateClient();

            var response = await PreflightAsync(client, OtherOrigin);

            response.Headers.Contains(AllowOriginHeader).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(keyRingPath))
                Directory.Delete(keyRingPath, recursive: true);
        }
    }

    private TestWebApplicationFactory CreateFactory(
        string environment = "Development",
        IReadOnlyDictionary<string, string?>? extraConfiguration = null
    ) => new(_fixture.ConnectionString, environment, extraConfiguration);

    private static async Task<HttpResponseMessage> PreflightAsync(HttpClient client, string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/stores");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        return await client.SendAsync(request);
    }
}
