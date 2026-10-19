using FluentAssertions;
using NSubstitute;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;

namespace Core.Application.UnitTests.Identity;

public sealed class SetUserLocaleTests
{
    private readonly ISingleUserProvider _userProvider = Substitute.For<ISingleUserProvider>();
    private readonly SetUserLocale _sut;

    public SetUserLocaleTests()
    {
        _sut = new SetUserLocale(_userProvider);
    }

    [Fact]
    public async Task ExecuteAsync_sets_the_locale_and_persists()
    {
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.ExecuteAsync(SupportedLocales.PtBr);

        result.IsSuccess.Should().BeTrue();
        user.Locale.Should().Be(SupportedLocales.PtBr);
        await _userProvider.Received(1).SaveAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_returns_failure_when_user_not_found()
    {
        _userProvider.GetAsync(Arg.Any<CancellationToken>()).Returns((ApplicationUser?)null);

        var result = await _sut.ExecuteAsync(SupportedLocales.PtBr);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Users.NotFound);
        await _userProvider
            .DidNotReceive()
            .SaveAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }
}
