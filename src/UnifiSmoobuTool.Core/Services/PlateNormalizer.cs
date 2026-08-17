namespace UnifiSmoobuTool.Core.Services;

/// <summary>
/// Reduces a guest-typed license plate to letters and digits only, and strips a leading
/// country/land indicator (e.g. "NL-", "D ", "B-") when one of the configured prefixes matches.
/// </summary>
public static class PlateNormalizer
{
    public static string Normalize(string rawInput, IReadOnlyCollection<string>? countryPrefixes = null)
    {
        ArgumentNullException.ThrowIfNull(rawInput);

        var working = rawInput.Trim();

        // Only strip a leading country/land indicator when it appears as its own token in the
        // guest's raw text (immediately followed by a space/hyphen/other delimiter). Stripping it
        // blindly from the fully-cleaned string would also eat the first letter of countless real
        // plates that happen to start with a single-letter code like "B", "D", "F", or "A".
        if (countryPrefixes is { Count: > 0 })
        {
            foreach (var prefix in countryPrefixes
                         .Where(p => !string.IsNullOrWhiteSpace(p))
                         .Select(p => p.Trim())
                         .OrderByDescending(p => p.Length))
            {
                if (working.Length > prefix.Length &&
                    working.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    !char.IsLetterOrDigit(working[prefix.Length]))
                {
                    working = working[(prefix.Length + 1)..].TrimStart();
                    break;
                }
            }
        }

        return new string(working.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }
}
