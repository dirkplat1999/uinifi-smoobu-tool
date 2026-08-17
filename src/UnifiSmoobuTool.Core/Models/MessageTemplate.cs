namespace UnifiSmoobuTool.Core.Models;

/// <summary>The guest-info-request message body for a given language, e.g. "en", "nl", "de".</summary>
public sealed class MessageTemplate
{
    public required string LanguageCode { get; init; }
    public required string Body { get; init; }
}
