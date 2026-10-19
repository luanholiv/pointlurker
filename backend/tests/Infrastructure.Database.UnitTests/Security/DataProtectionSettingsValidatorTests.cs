using FluentAssertions;
using PointLurker.Infrastructure.Database.Security;

namespace Infrastructure.Database.UnitTests.Security;

public sealed class DataProtectionSettingsValidatorTests
{
    private readonly DataProtectionSettingsValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        var result = _validator.Validate(null, new DataProtectionSettings());

        result.Succeeded.Should().BeTrue();
    }

    /// <summary>The relative path <c>appsettings.Development.json</c> uses must keep validating.</summary>
    [Fact]
    public void A_relative_path_is_valid()
    {
        var options = new DataProtectionSettings { KeyRingPath = ".dataprotection-keys" };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void An_unresolvable_path_fails()
    {
        var options = new DataProtectionSettings { KeyRingPath = "/app/keys\0/nested" };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.FailureMessage.Should().Contain("KeyRingPath");
    }

    /// <summary>
    /// Blank is rejected by the <c>[Required]</c> annotation, which runs first. The validator stays
    /// quiet so the failure is reported once, not twice.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_path_is_left_to_the_data_annotation(string keyRingPath)
    {
        var options = new DataProtectionSettings { KeyRingPath = keyRingPath };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }
}
