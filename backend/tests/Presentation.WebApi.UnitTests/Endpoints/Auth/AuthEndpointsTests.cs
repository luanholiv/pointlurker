using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using PointLurker.Core.Domain.Identity;
using PointLurker.Presentation.WebApi.Endpoints.Auth;
using PointLurker.Presentation.WebApi.Endpoints.Auth.Contracts;

namespace Presentation.WebApi.UnitTests.Endpoints.Auth;

// Authentication itself is served by the framework Identity API (MapIdentityApi); the only custom
// endpoint left under /api/auth is the anonymous bootstrap gate.
public sealed class AuthEndpointsTests
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthEndpointsTests()
    {
        var userStore = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            userStore,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }

    [Fact]
    public async Task Bootstrap_reports_false_when_no_user_exists()
    {
        _userManager.Users.Returns(Enumerable.Empty<ApplicationUser>().AsAsyncQueryable());

        var result = await AuthEndpoints.GetBootstrapStateAsync(
            _userManager,
            CancellationToken.None
        );

        var value = (BootstrapResponse)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        value.UserExists.Should().BeFalse();
    }

    [Fact]
    public async Task Bootstrap_reports_true_when_a_user_exists()
    {
        _userManager.Users.Returns(new[] { new ApplicationUser() }.AsAsyncQueryable());

        var result = await AuthEndpoints.GetBootstrapStateAsync(
            _userManager,
            CancellationToken.None
        );

        var value = (BootstrapResponse)
            result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
        value.UserExists.Should().BeTrue();
    }
}
