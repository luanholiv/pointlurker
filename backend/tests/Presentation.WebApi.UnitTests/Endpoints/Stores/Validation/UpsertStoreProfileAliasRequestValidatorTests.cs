using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Validation;

public class UpsertStoreProfileAliasRequestValidatorTests
{
    private readonly UpsertStoreProfileAliasRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_fully_populated_request()
    {
        var result = _validator.Validate(new UpsertStoreProfileAliasRequest("Your Name", "name"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_raw_label(string rawLabel)
    {
        var result = _validator.Validate(new UpsertStoreProfileAliasRequest(rawLabel, "name"));

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "RawLabel" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_canonical_label(string labelCanonical)
    {
        var result = _validator.Validate(
            new UpsertStoreProfileAliasRequest("Your Name", labelCanonical)
        );

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "LabelCanonical" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }
}
