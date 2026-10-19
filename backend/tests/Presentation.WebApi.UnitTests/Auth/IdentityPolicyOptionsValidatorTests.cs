using FluentAssertions;
using PointLurker.Presentation.WebApi.Auth;

namespace Presentation.WebApi.UnitTests.Auth;

public sealed class IdentityPolicyOptionsValidatorTests
{
    private readonly IdentityPolicyOptionsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        var result = _validator.Validate(null, new IdentityPolicyOptions());

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Lockout_duration_below_one_fails(int minutes)
    {
        var options = new IdentityPolicyOptions { LockoutDurationMinutes = minutes };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("LockoutDurationMinutes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Lockout_max_failed_attempts_below_one_fails(int attempts)
    {
        var options = new IdentityPolicyOptions { LockoutMaxFailedAttempts = attempts };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("LockoutMaxFailedAttempts");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-8)]
    public void Password_minimum_length_below_one_fails(int length)
    {
        var options = new IdentityPolicyOptions { PasswordMinimumLength = length };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("PasswordMinimumLength");
    }

    [Fact]
    public void Custom_values_above_the_minimum_succeed()
    {
        var options = new IdentityPolicyOptions
        {
            LockoutDurationMinutes = 30,
            LockoutMaxFailedAttempts = 10,
            PasswordMinimumLength = 12,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
