using FluentAssertions;
using PointLurker.Presentation.WebApi.Cors;

namespace Presentation.WebApi.UnitTests.Cors;

public sealed class CorsSettingsValidatorTests
{
    private readonly CorsSettingsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        var result = _validator.Validate(null, new CorsSettings());

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Empty_list_is_valid()
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = [] });

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("http://localhost:5173")]
    [InlineData("https://app.example.com:8443")]
    [InlineData("  https://app.example.com  ")]
    public void Valid_origins_pass(string origin)
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = [origin] });

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Blank_entries_are_ignored()
    {
        var options = new CorsSettings { AllowedOrigins = ["", "   ", "https://app.example.com"] };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("monitor.example.com")]
    [InlineData("https://app.example.com/")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com?a=1")]
    [InlineData("https://app.example.com#frag")]
    [InlineData("ftp://app.example.com")]
    [InlineData("https://user:pass@app.example.com")]
    public void Invalid_origins_fail(string origin)
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = [origin] });

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("AllowedOrigins[0]");
    }

    [Fact]
    public void Wildcard_failure_explains_how_to_disable_cors()
    {
        var result = _validator.Validate(null, new CorsSettings { AllowedOrigins = ["*"] });

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("leave the list empty");
    }

    [Fact]
    public void Trailing_slash_failure_is_explicit()
    {
        var options = new CorsSettings { AllowedOrigins = ["https://app.example.com/"] };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("trailing");
    }

    [Fact]
    public void All_invalid_origins_are_reported()
    {
        var options = new CorsSettings
        {
            AllowedOrigins = ["https://good.example.com", "nope", "ftp://bad.example.com"],
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().HaveCount(2);
        result.FailureMessage.Should().Contain("AllowedOrigins[1]");
        result.FailureMessage.Should().Contain("AllowedOrigins[2]");
    }
}
