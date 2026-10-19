using FluentAssertions;
using PointLurker.Core.Application.Identity;

namespace Core.Application.UnitTests.Identity;

public sealed class CredentialExpiryOptionsValidatorTests
{
    private readonly CredentialExpiryOptionsValidator _validator = new();

    [Fact]
    public void The_defaults_are_valid()
    {
        _validator.Validate(null, new CredentialExpiryOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void The_default_window_is_two_days()
    {
        new CredentialExpiryOptions().ExpiryWarningHours.Should().Be(48);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_window_that_is_not_positive_is_rejected(int hours)
    {
        var result = _validator.Validate(
            null,
            new CredentialExpiryOptions { ExpiryWarningHours = hours }
        );

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ExpiryWarningHours");
    }
}
