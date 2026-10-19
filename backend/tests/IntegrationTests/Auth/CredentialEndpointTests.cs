using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Auth;

[Collection(ContainersCollection.Name)]
public sealed class CredentialEndpointTests : IAsyncLifetime, IDisposable
{
    private const string Route = "/api/users/me/credentials/streamelements";
    private const string Secret = "eyJhbGciOiJIUzI1NiJ9.not-a-real-token.signature";

    private readonly DatabaseFixture _fixture;
    private readonly IStoreProviderClient _seApi = Substitute.For<IStoreProviderClient>();
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private string _accessToken = string.Empty;

    public CredentialEndpointTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seApi.Provider.Returns(StoreProvider.StreamElements);
        _seApi.GetAccountNameAsync(Secret, Arg.Any<CancellationToken>()).Returns("owner-se");
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString)
        {
            MockStreamElementsApi = _seApi,
        };
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task Set_and_clear_updates_the_credential_list()
    {
        await RegisterAndLoginAsync();

        var me = await GetCurrentUserAsync();
        me!.Credentials.Should().BeEmpty();

        var setResponse = await AuthenticatedSendAsync(
            HttpMethod.Put,
            Route,
            new { secret = Secret }
        );
        setResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        me = await GetCurrentUserAsync();
        ProvidersOf(me!).Should().Equal("streamelements");

        var clearResponse = await AuthenticatedSendAsync(HttpMethod.Delete, Route);
        clearResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        me = await GetCurrentUserAsync();
        me!.Credentials.Should().BeEmpty();
    }

    [Fact]
    public async Task A_saved_credential_is_encrypted_and_confirmed()
    {
        await RegisterAndLoginAsync();

        await AuthenticatedSendAsync(HttpMethod.Put, Route, new { secret = Secret });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var credential = await db.ProviderCredentials.SingleAsync();

        credential.Provider.Should().Be(StoreProvider.StreamElements);
        credential.EncryptedSecret.Should().NotBe(Secret).And.NotContain(Secret);
        credential.AccountName.Should().Be("owner-se");
        credential.LastValidatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Saving_again_replaces_the_credential_rather_than_adding_one()
    {
        await RegisterAndLoginAsync();

        await AuthenticatedSendAsync(HttpMethod.Put, Route, new { secret = Secret });
        await AuthenticatedSendAsync(HttpMethod.Put, Route, new { secret = Secret });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.ProviderCredentials.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_credential_the_provider_refuses_answers_422_and_is_not_stored()
    {
        await RegisterAndLoginAsync();
        _seApi
            .GetAccountNameAsync(Secret, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(401, "Unauthorized", "refused"));

        var response = await AuthenticatedSendAsync(HttpMethod.Put, Route, new { secret = Secret });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        CodeOf(body).Should().Be(ErrorCodes.Credentials.Rejected);
        body.Should().NotContain(Secret);
        (await GetCurrentUserAsync())!.Credentials.Should().BeEmpty();
    }

    [Fact]
    public async Task A_credential_the_check_cannot_reach_is_stored_unconfirmed()
    {
        await RegisterAndLoginAsync();
        _seApi
            .GetAccountNameAsync(Secret, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ProviderApiException(ProviderApiException.NoResponse, null, "down"));

        var response = await AuthenticatedSendAsync(HttpMethod.Put, Route, new { secret = Secret });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.ProviderCredentials.SingleAsync()).LastValidatedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_blank_secret_answers_400_with_the_credential_code_and_no_upstream_call()
    {
        await RegisterAndLoginAsync();

        var response = await AuthenticatedSendAsync(HttpMethod.Put, Route, new { secret = " " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var entry = json.RootElement.GetProperty("params").GetProperty("errors")[0];
        entry.GetProperty("field").GetString().Should().Be("secret");
        entry.GetProperty("code").GetString().Should().Be(ErrorCodes.Credentials.SecretRequired);
        await _seApi
            .DidNotReceive()
            .GetAccountNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("GauPoints")]
    [InlineData("0")]
    public async Task A_provider_that_holds_no_credential_answers_404(string provider)
    {
        await RegisterAndLoginAsync();
        var route = $"/api/users/me/credentials/{provider}";

        var put = await AuthenticatedSendAsync(HttpMethod.Put, route, new { secret = Secret });
        var delete = await AuthenticatedSendAsync(HttpMethod.Delete, route);

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await put.Content.ReadAsStringAsync();
        CodeOf(body).Should().Be(ErrorCodes.Credentials.ProviderUnsupported);
        body.Should().NotContain(Secret);
    }

    [Fact]
    public async Task Set_returns_unauthorized_without_auth()
    {
        var response = await _client.PutAsJsonAsync(Route, new { secret = "test" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string? CodeOf(string body)
    {
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("code").GetString();
    }

    private async Task RegisterAndLoginAsync()
    {
        await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "test@example.com", password = "Password123!" }
        );
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "test@example.com", password = "Password123!" }
        );
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        _accessToken = loginBody!.AccessToken;
    }

    private async Task<CurrentUserResponse?> GetCurrentUserAsync()
    {
        var response = await AuthenticatedSendAsync(HttpMethod.Get, "/api/users/me");
        return await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
    }

    private async Task<HttpResponseMessage> AuthenticatedSendAsync(
        HttpMethod method,
        string url,
        object? payload = null
    )
    {
        var request = new HttpRequestMessage(method, url);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        return await _client.SendAsync(request);
    }

    private sealed record LoginResponse(
        string AccessToken,
        string RefreshToken,
        string TokenType,
        int ExpiresIn
    );

    private static IEnumerable<string?> ProvidersOf(CurrentUserResponse me) =>
        me.Credentials.Select(credential => credential.GetProperty("provider").GetString());

    private sealed record CurrentUserResponse(
        Guid UserId,
        string Email,
        IReadOnlyList<JsonElement> Credentials
    );
}
