using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;
using PointLurker.Infrastructure.Database;

namespace IntegrationTests.Migrations;

/// <summary>
/// Verifies the AddUserLocale migration: default is en-US, CHECK constraint rejects unsupported locales.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class UserLocaleMigrationTests : IAsyncLifetime, IDisposable
{
    private readonly DatabaseFixture _fixture;
    private IdentityDbContext _identityDb = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private IServiceScope _scope = null!;
    private IServiceProvider _services = null!;

    public UserLocaleMigrationTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<IdentityDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<IdentityDbContext>();

        _services = services.BuildServiceProvider();
        _scope = _services.CreateScope();
        _identityDb = _scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _scope?.Dispose();
        (_services as IDisposable)?.Dispose();
    }

    [Fact]
    public async Task New_user_gets_default_locale_en_US()
    {
        var user = new ApplicationUser
        {
            UserName = "test@example.com",
            Email = "test@example.com",
        };
        var result = await _userManager.CreateAsync(user, "Password123!");
        result.Succeeded.Should().BeTrue();

        _identityDb.ChangeTracker.Clear();
        var loaded = await _identityDb.Users.FindAsync(user.Id);

        loaded!.Locale.Should().Be(SupportedLocales.EnUs);
    }

    [Fact]
    public async Task User_locale_can_be_set_to_pt_BR_and_persisted()
    {
        var user = new ApplicationUser
        {
            UserName = "test2@example.com",
            Email = "test2@example.com",
        };
        await _userManager.CreateAsync(user, "Password123!");

        user.SetLocale(SupportedLocales.PtBr);
        await _userManager.UpdateAsync(user);

        _identityDb.ChangeTracker.Clear();
        var loaded = await _identityDb.Users.FindAsync(user.Id);

        loaded!.Locale.Should().Be(SupportedLocales.PtBr);
    }

    [Fact]
    public async Task User_locale_check_constraint_rejects_unsupported_value()
    {
        var user = new ApplicationUser
        {
            UserName = "test3@example.com",
            Email = "test3@example.com",
        };
        await _userManager.CreateAsync(user, "Password123!");

        user.SetLocale("xx-XX");
        var act = () => _userManager.UpdateAsync(user);

        // The CHECK constraint is at the DB level; EF SaveChanges will throw
        await act.Should()
            .ThrowAsync<Exception>(
                "CHECK constraint CK_AspNetUsers_Locale_Allowlist rejects xx-XX"
            );
    }
}
