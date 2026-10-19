using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Identity;
using PointLurker.Presentation.WebApi.Auth;

namespace IntegrationTests.Auth;

[Collection(ContainersCollection.Name)]
public sealed class SingleUserValidatorIntegrationTests : IAsyncLifetime, IDisposable
{
    private const string Password = "Password123!";
    private readonly DatabaseFixture _fixture;
    private TestWebApplicationFactory _factory = null!;

    public SingleUserValidatorIntegrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _factory = new TestWebApplicationFactory(_fixture.ConnectionString);
        // Force the host to build so DI is fully wired.
        _ = _factory.Services;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory?.Dispose();

    [Fact]
    public async Task Validator_is_wired_into_identity_and_blocks_a_second_account()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var first = await userManager.CreateAsync(
            new ApplicationUser { UserName = "first@example.com", Email = "first@example.com" },
            Password
        );
        Assert.True(first.Succeeded, string.Join(", ", first.Errors.Select(e => e.Description)));

        var second = await userManager.CreateAsync(
            new ApplicationUser { UserName = "second@example.com", Email = "second@example.com" },
            Password
        );

        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, e => e.Code == SingleUserValidator.SingleUserErrorCode);

        var count = await userManager.Users.CountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Validator_allows_updating_the_sole_user()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = "sole@example.com",
            Email = "sole@example.com",
        };
        var create = await userManager.CreateAsync(user, Password);
        Assert.True(create.Succeeded);

        user.SetLocale("pt-BR");
        var update = await userManager.UpdateAsync(user);

        Assert.True(update.Succeeded, string.Join(", ", update.Errors.Select(e => e.Description)));
    }
}
