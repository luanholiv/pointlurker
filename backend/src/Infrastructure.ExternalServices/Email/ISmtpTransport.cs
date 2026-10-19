using MimeKit;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Thin seam over the MailKit SMTP client. One call performs a full
/// connect → authenticate → send → disconnect cycle (the MailKit client is not
/// reusable across concurrent sends). Mockable so <see cref="SmtpEmailService"/>'s
/// message mapping and retry logic can be unit-tested without a real SMTP server.
/// </summary>
public interface ISmtpTransport
{
    Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default);
}
