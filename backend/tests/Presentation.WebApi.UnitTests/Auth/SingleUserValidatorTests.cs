using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using PointLurker.Core.Domain.Identity;
using PointLurker.Presentation.WebApi.Auth;

namespace Presentation.WebApi.UnitTests.Auth;

public sealed class SingleUserValidatorTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SingleUserValidator _validator = new();

    public SingleUserValidatorTests()
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
    public async Task First_create_succeeds_when_no_users_exist()
    {
        _userManager.Users.Returns(Enumerable.Empty<ApplicationUser>().AsAsyncQueryable());
        var newUser = new ApplicationUser { Id = Guid.NewGuid() };

        var result = await _validator.ValidateAsync(_userManager, newUser);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Second_create_fails_with_single_user_error()
    {
        var existing = new ApplicationUser { Id = Guid.NewGuid() };
        _userManager.Users.Returns(new[] { existing }.AsAsyncQueryable());
        var newUser = new ApplicationUser { Id = Guid.NewGuid() };

        var result = await _validator.ValidateAsync(_userManager, newUser);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == SingleUserValidator.SingleUserErrorCode);
    }

    [Fact]
    public async Task Update_of_sole_user_succeeds()
    {
        var soleUser = new ApplicationUser { Id = Guid.NewGuid() };
        _userManager.Users.Returns(new[] { soleUser }.AsAsyncQueryable());

        var result = await _validator.ValidateAsync(_userManager, soleUser);

        result.Succeeded.Should().BeTrue();
    }
}
