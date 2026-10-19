using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Validation;

/// <summary>
/// Both StreamElements fields reach a value-object constructor that throws on blank. The validator
/// is what turns that into a per-field 400 instead of the 500 an escaped <c>ArgumentException</c>
/// produces. A GauPoints request carries no identity at all.
/// </summary>
public class CreateStoreRequestValidatorTests
{
    private readonly CreateStoreRequestValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("streamelements")]
    public void Accepts_a_fully_populated_streamelements_request(string? provider)
    {
        var result = _validator.Validate(new CreateStoreRequest(provider, "channel-1", "My Store"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_a_blank_channel_id(string? channelId)
    {
        var result = _validator.Validate(new CreateStoreRequest(null, channelId, "My Store"));

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "ChannelId" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_a_blank_display_name(string? displayName)
    {
        var result = _validator.Validate(new CreateStoreRequest(null, "channel-1", displayName));

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "DisplayName" && e.ErrorCode == ErrorCodes.Validation.Required
            );
    }

    [Fact]
    public void Reports_both_fields_when_both_are_blank()
    {
        var result = _validator.Validate(new CreateStoreRequest(null, "", ""));

        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Accepts_a_gaupoints_request_with_no_identity()
    {
        _validator
            .Validate(new CreateStoreRequest("gaupoints", null, null))
            .IsValid.Should()
            .BeTrue();
    }

    /// <summary>
    /// The server resolves the fixed store's identity itself. A client-sent one is refused, not
    /// ignored, so a caller learns it was never going to be used.
    /// </summary>
    [Fact]
    public void Refuses_a_client_sent_identity_for_gaupoints()
    {
        var result = _validator.Validate(
            new CreateStoreRequest("gaupoints", "someone-else", "Fake")
        );

        result
            .Errors.Should()
            .HaveCount(2)
            .And.OnlyContain(e => e.ErrorCode == ErrorCodes.Stores.IdentityNotAccepted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("twitch")]
    [InlineData("1")]
    public void Refuses_a_provider_that_does_not_parse(string provider)
    {
        var result = _validator.Validate(new CreateStoreRequest(provider, "channel-1", "My Store"));

        result
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "Provider" && e.ErrorCode == ErrorCodes.Stores.ProviderUnsupported
            );
    }
}
