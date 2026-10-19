using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Validation;

public class StoreLookupQueryValidatorTests
{
    private readonly StoreLookupQueryValidator _validator = new();

    [Fact]
    public void Accepts_a_streamelements_lookup_with_a_query()
    {
        _validator
            .Validate(new StoreLookupQuery("streamelements", "xqc"))
            .IsValid.Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("anything")]
    public void A_gaupoints_lookup_takes_any_query_or_none(string? query)
    {
        _validator.Validate(new StoreLookupQuery("gaupoints", query)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void A_streamelements_lookup_needs_a_query(string? query)
    {
        _validator
            .Validate(new StoreLookupQuery("streamelements", query))
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "Query" && e.ErrorCode == ErrorCodes.StoreLookup.QueryRequired
            );
    }

    /// <summary>
    /// The slug is the address; the enum's number is a persistence detail and never accepted, nor
    /// is a name in the wrong case.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("twitch")]
    [InlineData("0")]
    [InlineData("GauPoints")]
    public void A_provider_that_does_not_parse_is_refused(string? provider)
    {
        var errors = _validator.Validate(new StoreLookupQuery(provider, "xqc")).Errors;

        errors
            .Should()
            .ContainSingle(e =>
                e.PropertyName == "Provider" && e.ErrorCode == ErrorCodes.Stores.ProviderUnsupported
            );
    }
}
