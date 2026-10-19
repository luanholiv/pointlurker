using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Users.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Users.Validation;

public class SetLocaleRequestValidatorTests
{
    private readonly SetLocaleRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_populated_locale()
    {
        _validator.Validate(new SetLocaleRequest("en-US")).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_locale(string locale)
    {
        _validator
            .Validate(new SetLocaleRequest(locale))
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "Locale" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }

    /// <summary>
    /// The validator deliberately leaves the supported-set check to the handler — see
    /// <see cref="SetLocaleRequestValidator"/>.
    /// </summary>
    [Fact]
    public void Leaves_the_supported_locale_allowlist_to_the_handler()
    {
        _validator.Validate(new SetLocaleRequest("xx-YY")).IsValid.Should().BeTrue();
    }
}
