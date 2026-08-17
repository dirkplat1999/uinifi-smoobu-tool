using System.Text.RegularExpressions;

namespace UnifiSmoobuTool.Core.Services;

public sealed class GuestReplyParseResult
{
    public string? RawLicensePlate { get; init; }
    public string? PinCode { get; init; }

    /// <summary>True only when exactly one 4-digit PIN and exactly one plate-looking token were found.</summary>
    public bool IsConfident { get; init; }
}

/// <summary>
/// Best-effort extraction of a 4-digit PIN and a license-plate-looking token from a guest's free-text
/// reply. Ambiguous replies (more than one candidate, or a missing field) are surfaced as not confident
/// so the caller can route them to manual review instead of guessing.
/// </summary>
public static partial class GuestReplyParser
{
    [GeneratedRegex(@"\b\d{4}\b")]
    private static partial Regex PinRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9][A-Za-z0-9\-]{0,9}\b")]
    private static partial Regex TokenRegex();

    public static GuestReplyParseResult Parse(string replyText)
    {
        ArgumentNullException.ThrowIfNull(replyText);

        var pinMatches = PinRegex().Matches(replyText);
        string? pin = pinMatches.Count == 1 ? pinMatches[0].Value : null;

        var candidatePlates = TokenRegex()
            .Matches(replyText)
            .Select(m => m.Value)
            .Where(t => t.Any(char.IsLetter) && t.Any(char.IsDigit))
            .Where(t => pin is null || !string.Equals(t, pin, StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? plate = candidatePlates.Count >= 1 ? candidatePlates[0] : null;
        bool isConfident = pin is not null && candidatePlates.Count == 1;

        return new GuestReplyParseResult
        {
            RawLicensePlate = plate,
            PinCode = pin,
            IsConfident = isConfident,
        };
    }
}
