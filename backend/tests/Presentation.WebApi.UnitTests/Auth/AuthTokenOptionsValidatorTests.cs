using FluentAssertions;
using PointLurker.Presentation.WebApi.Auth;

namespace Presentation.WebApi.UnitTests.Auth;

public sealed class AuthTokenOptionsValidatorTests
{
    private readonly AuthTokenOptionsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        var result = _validator.Validate(null, new AuthTokenOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Access_minutes_below_one_fails(int minutes)
    {
        var options = new AuthTokenOptions { AccessTokenExpirationMinutes = minutes };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("AccessTokenExpirationMinutes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Refresh_hours_below_one_fails(int hours)
    {
        var options = new AuthTokenOptions { RefreshTokenExpirationHours = hours };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("RefreshTokenExpirationHours");
    }

    [Theory]
    // access == refresh (24h each) and access > refresh (25h vs 1h)
    [InlineData(1440, 24)]
    [InlineData(1500, 1)]
    public void Access_not_strictly_shorter_than_refresh_fails(int minutes, int hours)
    {
        var options = new AuthTokenOptions
        {
            AccessTokenExpirationMinutes = minutes,
            RefreshTokenExpirationHours = hours,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("shorter");
    }

    [Fact]
    public void Access_shorter_than_refresh_succeeds()
    {
        var options = new AuthTokenOptions
        {
            AccessTokenExpirationMinutes = 60,
            RefreshTokenExpirationHours = 2,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
