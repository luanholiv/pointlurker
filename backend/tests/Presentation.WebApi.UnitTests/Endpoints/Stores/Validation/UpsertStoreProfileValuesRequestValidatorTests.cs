using FluentAssertions;
using FluentValidation;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores.Validation;

public class UpsertStoreProfileValuesRequestValidatorTests
{
    private readonly UpsertStoreProfileValuesRequestValidator _validator = new();

    [Fact]
    public void Accepts_a_populated_collection()
    {
        var request = new UpsertStoreProfileValuesRequest([
            new StoreProfileValueUpsertRequest("name", "Ada"),
        ]);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// The handler reads a missing collection as "no changes" (<c>request.Values ?? []</c>), so an
    /// absent list is a valid request rather than a rejected one.
    /// </summary>
    [Fact]
    public void Accepts_an_absent_collection()
    {
        _validator.Validate(new UpsertStoreProfileValuesRequest(null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Rejects_an_element_with_a_blank_canonical_label()
    {
        var request = new UpsertStoreProfileValuesRequest([
            new StoreProfileValueUpsertRequest("", "Ada"),
        ]);

        _validator
            .Validate(request)
            .Errors.Should()
            .ContainSingle(e => e.ErrorCode == ErrorCodes.Validation.Required);
    }

    /// <summary>
    /// The element path is what tells the client which row failed, so it must survive into the
    /// failure rather than collapsing to the collection name.
    /// </summary>
    [Fact]
    public void Reports_the_failing_element_by_index()
    {
        var request = new UpsertStoreProfileValuesRequest([
            new StoreProfileValueUpsertRequest("name", "Ada"),
            new StoreProfileValueUpsertRequest("", "Bob"),
        ]);

        _validator
            .Validate(request)
            .Errors.Should()
            .ContainSingle(e => e.PropertyName == "Values[1].LabelCanonical");
    }

    [Fact]
    public void Rejects_an_element_value_over_the_cap()
    {
        var overCap = new string('x', UpsertStoreProfileValues.MaxValueLength + 1);
        var request = new UpsertStoreProfileValuesRequest([
            new StoreProfileValueUpsertRequest("name", overCap),
        ]);

        _validator
            .Validate(request)
            .Errors.Should()
            .ContainSingle(e =>
                e.PropertyName == "Values[0].Value" && e.ErrorCode == ErrorCodes.Validation.TooLong
            );
    }
}
