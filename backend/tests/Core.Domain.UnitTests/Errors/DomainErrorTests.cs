using FluentAssertions;
using PointLurker.Core.Domain.Errors;

namespace Core.Domain.UnitTests.Errors;

public sealed class DomainErrorTests
{
    [Fact]
    public void DomainError_stores_code_and_empty_params_by_default()
    {
        var error = new DomainError("users.not_found");

        error.Code.Should().Be("users.not_found");
        error.Params.Should().BeEmpty();
    }

    [Fact]
    public void DomainError_stores_code_and_provided_params()
    {
        var error = new DomainError(
            "stores.already_exists",
            new Dictionary<string, object?> { ["channelId"] = "xqc" }
        );

        error.Code.Should().Be("stores.already_exists");
        error.Params.Should().ContainKey("channelId").WhoseValue.Should().Be("xqc");
    }

    [Fact]
    public void DomainError_params_are_readonly()
    {
        var error = new DomainError(
            "wishlist.already_exists",
            new Dictionary<string, object?> { ["key"] = "value" }
        );

        error.Params.Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>();
    }

    [Fact]
    public void DomainError_with_null_param_value_is_allowed()
    {
        var error = new DomainError(
            "redemption.failure.token_decryption_failed",
            new Dictionary<string, object?> { ["context"] = null }
        );

        error.Params.Should().ContainKey("context").WhoseValue.Should().BeNull();
    }

    [Fact]
    public void DomainError_with_same_params_reference_are_equal()
    {
        var sharedParams = new Dictionary<string, object?> { ["field"] = "name" };

        var a = new DomainError("validation.required", sharedParams);
        var b = new DomainError("validation.required", sharedParams);

        // Records compare Params by reference (Dictionary doesn't override Equals).
        // Same reference → equal.
        a.Should().Be(b);
    }

    [Fact]
    public void DomainError_with_different_codes_are_not_equal()
    {
        var a = new DomainError("users.not_found");
        var b = new DomainError("stores.not_found");

        a.Should().NotBe(b);
    }
}
