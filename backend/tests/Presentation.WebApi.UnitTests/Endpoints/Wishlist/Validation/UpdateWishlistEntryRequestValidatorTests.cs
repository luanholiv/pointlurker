using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Wishlist.Validation;

public class UpdateWishlistEntryRequestValidatorTests
{
    private readonly UpdateWishlistEntryRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_populated_request()
    {
        _validator
            .Validate(new UpdateWishlistEntryRequest(true, false, "please"))
            .IsValid.Should()
            .BeTrue();
    }

    [Fact]
    public void Accepts_an_absent_redemption_message()
    {
        _validator
            .Validate(new UpdateWishlistEntryRequest(true, false, null))
            .IsValid.Should()
            .BeTrue();
    }

    [Fact]
    public void Accepts_a_redemption_message_at_the_cap()
    {
        var atCap = new string('x', WishlistEntry.RedemptionMessageMaxLength);

        _validator
            .Validate(new UpdateWishlistEntryRequest(true, false, atCap))
            .IsValid.Should()
            .BeTrue();
    }

    [Fact]
    public void Rejects_a_redemption_message_over_the_cap()
    {
        var overCap = new string('x', WishlistEntry.RedemptionMessageMaxLength + 1);

        _validator
            .Validate(new UpdateWishlistEntryRequest(true, false, overCap))
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "RedemptionMessage"
                && e.ErrorCode == ErrorCodes.Validation.TooLong
            );
    }

    [Fact]
    public void Defaults_the_desired_count_to_one_when_the_property_is_absent()
    {
        new UpdateWishlistEntryRequest(true, false, null).DesiredRedemptions.Should().Be(1);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(WishlistEntry.DesiredRedemptionsMax)]
    [InlineData(null)]
    public void Accepts_a_desired_count_in_range_and_unlimited(int? desired)
    {
        _validator
            .Validate(new UpdateWishlistEntryRequest(true, true, null, desired))
            .IsValid.Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(WishlistEntry.DesiredRedemptionsMax + 1)]
    public void Rejects_a_desired_count_out_of_range(int desired)
    {
        _validator
            .Validate(new UpdateWishlistEntryRequest(true, true, null, desired))
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "DesiredRedemptions"
                && e.ErrorCode == ErrorCodes.Validation.OutOfRange
            );
    }
}
