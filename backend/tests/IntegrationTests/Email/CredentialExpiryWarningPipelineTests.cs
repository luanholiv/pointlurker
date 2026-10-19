using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Email;

/// <summary>
/// Drives the expiry warning from a credential row in real Postgres to a message in a real SMTP
/// server, twice, to prove the "warned for this expiry" marker survives a commit and a fresh
/// context — the only thing that keeps a daily pass from mailing the owner every day.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class CredentialExpiryWarningPipelineTests(
    DatabaseFixture database,
    MailpitFixture mailpit
) : IAsyncLifetime, IDisposable
{
    private const string UserEmail = "owner@test.com";

    private TestWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await mailpit.Messages.ClearAsync();

        _factory = new TestWebApplicationFactory(
            database.ConnectionString,
            extraConfiguration: new Dictionary<string, string?>
            {
                ["Smtp:Host"] = mailpit.SmtpHost,
                ["Smtp:Port"] = mailpit.MappedSmtpPort.ToString(),
                ["Smtp:Security"] = "None",
                ["Smtp:Username"] = "test",
                ["Smtp:Password"] = "test",
                ["Smtp:From"] = "monitor@example.com",
                ["Smtp:MaxRetries"] = "1",
                ["Smtp:RetryBaseDelaySeconds"] = "0",
            }
        );
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory?.Dispose();

    [Fact]
    public async Task An_expiring_session_is_mailed_once_across_passes()
    {
        // Whole seconds, as a JWT exp is: Postgres keeps microseconds, and the marker is compared
        // for equality against the expiry it read back.
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.AddHours(20).ToUnixTimeSeconds()
        );
        await SeedGauPointsCredentialAsync(expiresAt);

        await RunWarningPassAsync();
        await RunWarningPassAsync();

        var message = await mailpit.Messages.WaitForSingleMessageAsync();
        message.To.Should().ContainSingle().Which.Address.Should().Be(UserEmail);
        message.Subject.Should().Be("Your GauPoints sign-in expires soon");
        (message.Html ?? string.Empty).Should().Contain("paste it on the Settings page");
        (await mailpit.Messages.CountAsync()).Should().Be(1);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var credential = await db.ProviderCredentials.SingleAsync();
        credential.ExpiryWarningSentFor.Should().Be(expiresAt);
    }

    private async Task SeedGauPointsCredentialAsync(DateTimeOffset expiresAt)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = UserEmail, Email = UserEmail };
        (await users.CreateAsync(user, "Password123!")).Succeeded.Should().BeTrue();

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var credential = new ProviderCredential(
            user.Id,
            StoreProvider.GauPoints,
            "ciphertext",
            expiresAt.AddDays(-6)
        );
        credential.SetExpiresAt(expiresAt);
        db.ProviderCredentials.Add(credential);
        await db.SaveChangesAsync();
    }

    private async Task RunWarningPassAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WarnExpiringCredentials>().ExecuteAsync();
    }
}
