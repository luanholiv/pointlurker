using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;

namespace IntegrationTests.Identity;

[Collection(ContainersCollection.Name)]
public sealed class SingleUserProviderTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;

    public SingleUserProviderTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        _ = _factory.Services;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory?.Dispose();

    [Fact]
    public async Task GetAsync_returns_null_when_no_user_exists()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISingleUserProvider>();

        var user = await provider.GetAsync(CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_returns_the_owner_once_registered()
    {
        await SeedOwnerAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISingleUserProvider>();

        var user = await provider.GetAsync(CancellationToken.None);

        user.Should().NotBeNull();
        user!.Email.Should().Be("owner@example.com");
    }

    [Fact]
    public async Task SaveAsync_persists_mutations_to_the_owner()
    {
        await SeedOwnerAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<ISingleUserProvider>();
            var user = await provider.GetAsync(CancellationToken.None);

            user!.SetLocale(SupportedLocales.PtBr);
            await provider.SaveAsync(user, CancellationToken.None);
        }

        // A fresh scope means a fresh IdentityDbContext — the value must have hit the database,
        // not just the previous scope's change tracker.
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyProvider = verifyScope.ServiceProvider.GetRequiredService<ISingleUserProvider>();

        var reloaded = await verifyProvider.GetAsync(CancellationToken.None);

        reloaded.Should().NotBeNull();
        reloaded!.Locale.Should().Be(SupportedLocales.PtBr);
    }

    [Fact]
    public async Task SaveAsync_honours_a_cancelled_token()
    {
        await SeedOwnerAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISingleUserProvider>();
        var user = await provider.GetAsync(CancellationToken.None);
        user!.SetLocale(SupportedLocales.PtBr);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var save = async () => await provider.SaveAsync(user, cts.Token);

        await save.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetAsync_returns_a_tracked_instance_so_UserManager_writes_still_work()
    {
        await SeedOwnerAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var provider = scope.ServiceProvider.GetRequiredService<ISingleUserProvider>();
            var userManager = scope.ServiceProvider.GetRequiredService<
                UserManager<ApplicationUser>
            >();

            var user = await provider.GetAsync(CancellationToken.None);
            user!.SetLocale("pt-BR");

            var result = await userManager.UpdateAsync(user);

            result.Succeeded.Should().BeTrue();
        }

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var reloaded = await verifyScope
            .ServiceProvider.GetRequiredService<ISingleUserProvider>()
            .GetAsync(CancellationToken.None);

        reloaded!.Locale.Should().Be("pt-BR");
    }

    private async Task SeedOwnerAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "owner@example.com",
            Email = "owner@example.com",
        };

        var result = await userManager.CreateAsync(user, "Password123!");

        result.Succeeded.Should().BeTrue();
    }
}
