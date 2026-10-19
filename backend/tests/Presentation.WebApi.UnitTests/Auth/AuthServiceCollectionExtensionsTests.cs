using FluentAssertions;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PointLurker.Core.Domain.Identity;
using PointLurker.Presentation.WebApi.Auth;

namespace Presentation.WebApi.UnitTests.Auth;

public class AuthServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_authorization()
    {
        var services = BuildServices();

        services.Should().Contain(d => d.ServiceType == typeof(IAuthorizationService));
    }

    /// <summary>
    /// Order is the contract: <see cref="UserManager{TUser}"/> consumes
    /// <c>IEnumerable&lt;IUserValidator&lt;TUser&gt;&gt;</c>, so registering
    /// <see cref="SingleUserValidator"/> before Identity's own defaults would change which
    /// validator reports first and which error the register endpoint surfaces.
    /// </summary>
    [Fact]
    public void Registers_the_single_user_validator_after_the_framework_defaults()
    {
        var services = BuildServices();

        var userValidators = services
            .Where(d => d.ServiceType == typeof(IUserValidator<ApplicationUser>))
            .ToList();

        userValidators.Should().HaveCountGreaterThan(1);
        userValidators[^1].ImplementationType.Should().Be(typeof(SingleUserValidator));
    }

    [Fact]
    public void Registers_the_single_user_validator_as_scoped()
    {
        var services = BuildServices();

        var descriptor = services.Single(d => d.ImplementationType == typeof(SingleUserValidator));

        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Registers_the_auth_token_options_validator()
    {
        var services = BuildServices();

        services
            .Should()
            .Contain(d =>
                d.ServiceType == typeof(IValidateOptions<AuthTokenOptions>)
                && d.ImplementationType == typeof(AuthTokenOptionsValidator)
            );
    }

    [Fact]
    public void Registers_the_identity_policy_options_validator()
    {
        var services = BuildServices();

        services
            .Should()
            .Contain(d =>
                d.ServiceType == typeof(IValidateOptions<IdentityPolicyOptions>)
                && d.ImplementationType == typeof(IdentityPolicyOptionsValidator)
            );
    }

    /// <summary>
    /// Empty configuration must still produce these three values, or binding the account policy to
    /// options silently changed it.
    /// </summary>
    [Fact]
    public void Applies_the_default_identity_policy_when_nothing_is_configured()
    {
        var provider = BuildServices().BuildServiceProvider();

        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        identity.Lockout.DefaultLockoutTimeSpan.Should().Be(TimeSpan.FromMinutes(5));
        identity.Lockout.MaxFailedAccessAttempts.Should().Be(5);
        identity.Password.RequiredLength.Should().Be(8);
    }

    [Fact]
    public void Applies_the_configured_identity_policy()
    {
        var provider = BuildServices(
                new Dictionary<string, string?>
                {
                    ["Authentication:LockoutDurationMinutes"] = "15",
                    ["Authentication:LockoutMaxFailedAttempts"] = "3",
                    ["Authentication:PasswordMinimumLength"] = "12",
                }
            )
            .BuildServiceProvider();

        var identity = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        identity.Lockout.DefaultLockoutTimeSpan.Should().Be(TimeSpan.FromMinutes(15));
        identity.Lockout.MaxFailedAccessAttempts.Should().Be(3);
        identity.Password.RequiredLength.Should().Be(12);
    }

    [Fact]
    public void Rejects_a_password_minimum_length_below_one()
    {
        var provider = BuildServices(
                new Dictionary<string, string?> { ["Authentication:PasswordMinimumLength"] = "0" }
            )
            .BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<IdentityPolicyOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void Applies_the_configured_lifetimes_to_the_identity_bearer_scheme()
    {
        var provider = BuildServices(
                new Dictionary<string, string?>
                {
                    ["Authentication:AccessTokenExpirationMinutes"] = "45",
                    ["Authentication:RefreshTokenExpirationHours"] = "12",
                }
            )
            .BuildServiceProvider();

        var bearer = new BearerTokenOptions();

        // Only this project's post-configure step is invoked; the framework's own steps need a
        // fully wired data-protection stack that is out of scope for a unit test.
        var postConfigure = provider
            .GetServices<IPostConfigureOptions<BearerTokenOptions>>()
            .OfType<PostConfigureOptions<BearerTokenOptions, IOptions<AuthTokenOptions>>>()
            .Single();
        postConfigure.PostConfigure(IdentityConstants.BearerScheme, bearer);

        bearer.BearerTokenExpiration.Should().Be(TimeSpan.FromMinutes(45));
        bearer.RefreshTokenExpiration.Should().Be(TimeSpan.FromHours(12));
    }

    [Fact]
    public void Rejects_an_access_token_lifetime_that_outlives_the_refresh_token()
    {
        var provider = BuildServices(
                new Dictionary<string, string?>
                {
                    ["Authentication:AccessTokenExpirationMinutes"] = "120",
                    ["Authentication:RefreshTokenExpirationHours"] = "1",
                }
            )
            .BuildServiceProvider();

        var act = () => provider.GetRequiredService<IOptions<AuthTokenOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    private static IServiceCollection BuildServices(
        Dictionary<string, string?>? configuration = null
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build()
        );
        services.AddAppAuthentication();
        return services;
    }
}
