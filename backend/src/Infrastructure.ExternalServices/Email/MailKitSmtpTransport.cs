using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Production <see cref="ISmtpTransport"/> backed by MailKit's <see cref="SmtpClient"/>.
/// Opens a fresh connection per send (the client is not thread-safe or reusable).
/// </summary>
public sealed class MailKitSmtpTransport(IOptions<SmtpSettings> settings) : ISmtpTransport
{
    private readonly SmtpSettings _settings = settings.Value;

    public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            _settings.Security,
            cancellationToken
        );
        await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
