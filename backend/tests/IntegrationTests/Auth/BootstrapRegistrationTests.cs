using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Identity;

namespace IntegrationTests.Auth;

[Collection(ContainersCollection.Name)]
public sealed class BootstrapRegistrationTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public BootstrapRegistrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

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
    public async Task BootstrapEndpoint_returns_false_when_no_users_exist()
    {
        var responseMessage = await _client.GetAsync("/api/auth/bootstrap");
        var responseBody = await responseMessage.Content.ReadAsStringAsync();

        Assert.True(
            responseMessage.IsSuccessStatusCode,
            $"Unexpected status code {responseMessage.StatusCode}: {responseBody}"
        );

        var response = await responseMessage.Content.ReadFromJsonAsync<BootstrapResponse>();

        Assert.NotNull(response);
        Assert.False(response!.UserExists);
    }

    [Fact]
    public async Task BootstrapEndpoint_returns_true_after_registration()
    {
        await RegisterAsync("admin@example.com", "Password123!");

        var response = await _client.GetFromJsonAsync<BootstrapResponse>("/api/auth/bootstrap");

        Assert.NotNull(response);
        Assert.True(response!.UserExists);
    }

    [Fact]
    public async Task Register_succeeds_once_then_second_is_rejected_with_400()
    {
        var first = await RegisterAsync("admin@example.com", "Password123!");
        Assert.True(
            first.IsSuccessStatusCode,
            $"Unexpected status {first.StatusCode}: {await first.Content.ReadAsStringAsync()}"
        );

        // Second registration is blocked by the SingleUserValidator at the Identity core. The
        // framework register endpoint surfaces it as a 400 ValidationProblem (NOT the prior 403).
        var second = await RegisterAsync("second@example.com", "Password123!");
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var secondBody = await second.Content.ReadAsStringAsync();
        Assert.Contains(SingleUserErrorCode, secondBody);

        // Exactly one account exists.
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(1, await userManager.Users.CountAsync());
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/register", new { email, password });

    // Mirrors SingleUserValidator.SingleUserErrorCode (the Identity error code).
    private const string SingleUserErrorCode = "SingleUserOnly";

    private sealed record BootstrapResponse(bool UserExists);
}
