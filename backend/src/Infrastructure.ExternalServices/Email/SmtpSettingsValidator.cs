using Microsoft.Extensions.Options;
using MimeKit;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Validates <see cref="SmtpSettings"/> beyond what DataAnnotations express:
/// rejects whitespace-only required values and verifies <c>From</c> parses as a mailbox
/// (allowing the display-name form that <c>[EmailAddress]</c> would reject).
/// All failures are returned together so each is logged on startup.
/// </summary>
public sealed class SmtpSettingsValidator : IValidateOptions<SmtpSettings>
{
    // Require a domain so bare strings like "not-an-email" are rejected
    // (MimeKit allows domain-less addresses by default).
    private static readonly ParserOptions StrictAddress = CreateStrictAddressOptions();

    private static ParserOptions CreateStrictAddressOptions()
    {
        var options = ParserOptions.Default.Clone();
        options.AllowAddressesWithoutDomain = false;
        return options;
    }

    public ValidateOptionsResult Validate(string? name, SmtpSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Host))
            failures.Add("SmtpSettings: Host is required");

        if (string.IsNullOrWhiteSpace(options.Username))
            failures.Add("SmtpSettings: Username is required");

        if (string.IsNullOrWhiteSpace(options.Password))
            failures.Add("SmtpSettings: Password is required");

        if (string.IsNullOrWhiteSpace(options.From))
            failures.Add("SmtpSettings: From is required");
        else if (!MailboxAddress.TryParse(StrictAddress, options.From, out _))
            failures.Add("SmtpSettings: From must be a valid email address");

        if (options.Port is < 1 or > 65535)
            failures.Add("SmtpSettings: Port must be between 1 and 65535");

        // Polly refuses a MaxRetryAttempts below 1 and throws as the pipeline is built, which would
        // otherwise surface as an opaque construction failure rather than a named setting.
        if (options.MaxRetries < 1)
            failures.Add("SmtpSettings: MaxRetries must be at least 1");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
