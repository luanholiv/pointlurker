using FluentAssertions;
using MailKit.Security;
using PointLurker.Infrastructure.ExternalServices.Email;

namespace Infrastructure.ExternalServices.UnitTests.Email;

public sealed class SmtpSettingsValidatorTests
{
    private readonly SmtpSettingsValidator _validator = new();

    private static SmtpSettings ValidSettings() =>
        new()
        {
            Host = "smtp.example.com",
            Username = "user@example.com",
            Password = "secret",
            From = "noreply@example.com",
        };

    [Fact]
    public void Defaults_with_required_fields_set_are_valid()
    {
        var options = ValidSettings();

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
        // Defaults applied.
        options.Port.Should().Be(587);
        options.Security.Should().Be(SecureSocketOptions.StartTls);
        options.MaxRetries.Should().Be(3);
        options.RetryBaseDelaySeconds.Should().Be(2);
    }

    [Fact]
    public void From_with_display_name_is_valid()
    {
        var options = ValidSettings();
        options.From = "Monitor <noreply@example.com>";

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_host_fails(string? host)
    {
        var options = ValidSettings();
        options.Host = host!;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("Host"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_username_fails(string? username)
    {
        var options = ValidSettings();
        options.Username = username!;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("Username"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_password_fails(string? password)
    {
        var options = ValidSettings();
        options.Password = password!;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("Password"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_from_fails(string? from)
    {
        var options = ValidSettings();
        options.From = from!;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("From"));
    }

    [Fact]
    public void Non_email_from_fails()
    {
        var options = ValidSettings();
        options.From = "not-an-email";

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("From") && f.Contains("valid email"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Port_out_of_range_fails(int port)
    {
        var options = ValidSettings();
        options.Port = port;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("Port"));
    }

    /// <summary>
    /// Zero reads like "do not retry", so it is the value an operator would reach for. It has to be
    /// refused here, where the message can name the setting, because the retry pipeline refuses it
    /// too — and that refusal happens while the mail service is being constructed, long after
    /// configuration has bound cleanly.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MaxRetries_below_one_fails(int maxRetries)
    {
        var options = ValidSettings();
        options.MaxRetries = maxRetries;

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(f => f.Contains("MaxRetries"));
    }

    [Fact]
    public void Multiple_failures_are_all_reported()
    {
        var options = new SmtpSettings
        {
            Host = "",
            Username = "",
            Password = "",
            From = "",
            Port = 0,
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeFalse();
        // Host, Username, Password, From, Port -> 5 distinct failures.
        result.Failures.Should().HaveCount(5);
    }
}
