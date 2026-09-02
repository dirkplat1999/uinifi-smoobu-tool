using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Notifications;

public sealed class SmtpAlerter : IGuestEmailSender
{
    private readonly ILogger<SmtpAlerter> _logger;

    public SmtpAlerter(ILogger<SmtpAlerter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Sends to the fixed alert inbox configured in <see cref="SmtpSettings.ToAddress"/>
    /// (error alerts, test emails).</summary>
    public Task SendAsync(SmtpSettings settings, string subject, string body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return SendAsync(settings, settings.ToAddress, subject, body, ct);
    }

    /// <summary>Sends to an arbitrary recipient (guest-facing emails for manual bookings).</summary>
    public async Task SendAsync(SmtpSettings settings, string toAddress, string subject, string body, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(toAddress));
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
            _logger.LogError(ex, "Failed to send email to {ToAddress}.", toAddress);
        }
    }
}
