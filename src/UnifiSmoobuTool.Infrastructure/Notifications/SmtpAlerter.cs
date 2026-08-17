using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Notifications;

public sealed class SmtpAlerter
{
    private readonly ILogger<SmtpAlerter> _logger;

    public SmtpAlerter(ILogger<SmtpAlerter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendAsync(SmtpSettings settings, string subject, string body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(settings.ToAddress));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            var socketOptions = settings.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(settings.Username) && !string.IsNullOrEmpty(settings.Password))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password, ct).ConfigureAwait(false);
            }

            await client.SendAsync(message, ct).ConfigureAwait(false);
            await client.DisconnectAsync(true, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMTP alert email.");
        }
    }
}
