using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Validation;

public class StoreProfileValueUpsertRequestValidatorTests
{
    private readonly StoreProfileValueUpsertRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_populated_value()
    {
        var result = _validator.Validate(new StoreProfileValueUpsertRequest("name", "Ada"));

        result.IsValid.Should().BeTrue();
    }

    /// <summary>A null value is the documented way to remove a stored profile value.</summary>
    [Fact]
    public void Accepts_a_null_value()
    {
        var result = _validator.Validate(new StoreProfileValueUpsertRequest("name", null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_canonical_label(string labelCanonical)
    {
        var result = _validator.Validate(new StoreProfileValueUpsertRequest(labelCanonical, "Ada"));

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "LabelCanonical" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }

    [Fact]
    public void Accepts_a_value_at_the_cap()
    {
        var atCap = new string('x', UpsertStoreProfileValues.MaxValueLength);

        var result = _validator.Validate(new StoreProfileValueUpsertRequest("name", atCap));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_value_over_the_cap()
    {
        var overCap = new string('x', UpsertStoreProfileValues.MaxValueLength + 1);

        var result = _validator.Validate(new StoreProfileValueUpsertRequest("name", overCap));

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "Value" && e.ErrorCode == ErrorCodes.Validation.TooLong
            );
    }

    /// <summary>
    /// The cap has to reach the client as a number, because the catalog sentence interpolates it.
    /// FluentValidation supplies it as a placeholder; the mapper forwards it as <c>max</c>.
    /// </summary>
    [Fact]
    public void Publishes_the_cap_as_a_rule_placeholder()
    {
        var overCap = new string('x', UpsertStoreProfileValues.MaxValueLength + 1);

        var result = _validator.Validate(new StoreProfileValueUpsertRequest("name", overCap));

        result
            .Errors[0]
            .FormattedMessagePlaceholderValues["MaxLength"]
            .Should()
            .Be(UpsertStoreProfileValues.MaxValueLength);
    }
}
