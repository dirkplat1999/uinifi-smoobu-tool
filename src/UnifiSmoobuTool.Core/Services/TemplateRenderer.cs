using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Services;

public static class TemplateRenderer
{
    public static string Render(string template, IReadOnlyDictionary<string, string> placeholders)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(placeholders);

        var result = template;
        foreach (var (key, value) in placeholders)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>Picks the template of the given <paramref name="kind"/> matching the guest's
    /// language, falling back to the configured default language, and finally to whatever template
    /// of that kind exists first.</summary>
    public static MessageTemplate SelectTemplate(
        IReadOnlyCollection<MessageTemplate> templates,
        MessageTemplateKind kind,
        string? guestLanguageCode,
        string defaultLanguageCode)
    {
        ArgumentNullException.ThrowIfNull(templates);
        var candidates = templates.Where(t => t.Kind == kind).ToList();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException($"No \"{kind}\" message templates are configured.");
        }

        if (!string.IsNullOrWhiteSpace(guestLanguageCode))
        {
            var match = candidates.FirstOrDefault(t =>
                string.Equals(t.LanguageCode, guestLanguageCode, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        var fallback = candidates.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, defaultLanguageCode, StringComparison.OrdinalIgnoreCase));

        return fallback ?? candidates[0];
    }
}
