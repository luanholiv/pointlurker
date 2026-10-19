using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace PointLurker.Infrastructure.ExternalServices.Email;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    // Not [EmailAddress]: the From may carry a display name (e.g. "Monitor <noreply@example.com>"),
    // which that attribute rejects. Format is validated via MimeKit parsing in SmtpSettingsValidator.
    [Required]
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// TLS mode. Default <see cref="SecureSocketOptions.StartTls"/> (submission port 587).
    /// Use <see cref="SecureSocketOptions.SslOnConnect"/> for implicit-TLS port 465.
    /// </summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;

    /// <summary>
    /// Retry attempts for a transient send failure, in addition to the original call.
    /// <para>
    /// The floor is 1, not 0: this feeds Polly's <c>MaxRetryAttempts</c>, which refuses anything
    /// below 1 and throws while the retry pipeline is being built. A 0 accepted here would bind
    /// cleanly and then kill the process on startup, so it is rejected where the message can still
    /// name the setting. To disable retrying, there is nothing to set — a single attempt is the
    /// floor, and the send either succeeds or is recorded as failed.
    /// </para>
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRetries { get; set; } = 3;

    [Range(0, int.MaxValue)]
    public int RetryBaseDelaySeconds { get; set; } = 2;
}
