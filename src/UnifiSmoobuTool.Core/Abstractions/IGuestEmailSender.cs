using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

/// <summary>Sends a guest-facing email (used for manual bookings, which have no Smoobu message
/// thread to send through instead).</summary>
public interface IGuestEmailSender
{
    Task SendAsync(SmtpSettings settings, string toAddress, string subject, string body, CancellationToken ct = default);
}
