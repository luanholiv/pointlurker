using FluentAssertions;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace Infrastructure.ExternalServices.UnitTests.Email;

public sealed class AppUrlSettingsValidatorTests
{
    private readonly AppUrlSettingsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unset_url_is_valid_and_normalises_to_null(string? value)
    {
        var options = new AppUrlSettings { PublicBaseUrl = value };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();

        // The template branches on null alone, so blank has to collapse into it here rather than
        // leaving every call site to repeat the whitespace check.
        options.PublicBaseUrl.Should().BeNull();
    }

    [Theory]
    [InlineData("https://monitor.example.com")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://monitor.example.com:8443")]
    public void An_origin_is_valid(string value)
    {
        var result = _validator.Validate(null, new AppUrlSettings { PublicBaseUrl = value });

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://monitor.example.com/", "slash")]
    [InlineData("https://monitor.example.com/app", "path")]
    [InlineData("https://monitor.example.com?a=b", "query")]
    [InlineData("https://monitor.example.com#top", "fragment")]
    public void Anything_after_the_authority_is_rejected(string value, string because)
    {
        var result = _validator.Validate(null, new AppUrlSettings { PublicBaseUrl = value });

        result
            .Failed.Should()
            .BeTrue($"a {because} would double up when the template appends its own path");
    }

    [Theory]
    [InlineData("monitor.example.com")]
    [InlineData("/wishlist")]
    [InlineData("ftp://monitor.example.com")]
    public void A_value_that_is_not_an_http_origin_is_rejected(string value)
    {
        var result = _validator.Validate(null, new AppUrlSettings { PublicBaseUrl = value });

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Every_problem_with_one_value_is_reported_together()
    {
        var result = _validator.Validate(
            null,
            new AppUrlSettings { PublicBaseUrl = "https://monitor.example.com/app?a=b#top" }
        );

        result.Failures.Should().HaveCount(3);
    }
}
