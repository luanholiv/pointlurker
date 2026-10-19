using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Validation;

public class CreateWishlistEntryRequestValidatorTests
{
    private readonly CreateWishlistEntryRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_populated_request()
    {
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, false, "please");

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Accepts_an_absent_redemption_message()
    {
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, false, null);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_an_empty_store_item_id()
    {
        var request = new CreateWishlistEntryRequest(Guid.Empty, true, false, null);

        _validator
            .Validate(request)
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "StoreItemId" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }

    [Fact]
    public void Accepts_a_redemption_message_at_the_cap()
    {
        var atCap = new string('x', WishlistEntry.RedemptionMessageMaxLength);
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, false, atCap);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_a_redemption_message_over_the_cap()
    {
        var overCap = new string('x', WishlistEntry.RedemptionMessageMaxLength + 1);
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, false, overCap);

        _validator
            .Validate(request)
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "RedemptionMessage"
                && e.ErrorCode == ErrorCodes.Validation.TooLong
            );
    }

    [Fact]
    public void Defaults_the_desired_count_to_one_when_the_property_is_absent()
    {
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, false, null);

        request.DesiredRedemptions.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(WishlistEntry.DesiredRedemptionsMax)]
    [InlineData(null)]
    public void Accepts_a_desired_count_in_range_and_unlimited(int? desired)
    {
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, true, null, desired);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WishlistEntry.DesiredRedemptionsMax + 1)]
    public void Rejects_a_desired_count_out_of_range(int desired)
    {
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, true, null, desired);

        _validator
            .Validate(request)
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "DesiredRedemptions"
                && e.ErrorCode == ErrorCodes.Validation.OutOfRange
            );
    }

    [Fact]
    public void Publishes_both_bounds_as_rule_placeholders()
    {
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, true, null, 0);

        var placeholders = _validator.Validate(request).Errors[0].FormattedMessagePlaceholderValues;

        placeholders["From"].Should().Be(1);
        placeholders["To"].Should().Be(WishlistEntry.DesiredRedemptionsMax);
    }

    [Fact]
    public void Publishes_the_cap_as_a_rule_placeholder()
    {
        var overCap = new string('x', WishlistEntry.RedemptionMessageMaxLength + 1);
        var request = new CreateWishlistEntryRequest(Guid.NewGuid(), true, false, overCap);

        _validator
            .Validate(request)
            .Errors[0]
            .FormattedMessagePlaceholderValues["MaxLength"]
            .Should()
            .Be(WishlistEntry.RedemptionMessageMaxLength);
    }
}
