namespace UnifiSmoobuTool.Core.Models;

/// <summary>Which point in the guest-messaging flow a template is used for.</summary>
public enum MessageTemplateKind
{
    /// <summary>The initial ask, sent a configurable number of days before arrival.</summary>
    Request,

    /// <summary>Sent when the guest's reply couldn't be confidently read (missing or ambiguous
    /// plate/PIN), asking them to resend it clearly.</summary>
    Clarification,

    /// <summary>Sent when the guest's reply was read clearly, acknowledging receipt.</summary>
    Confirmation,
}

/// <summary>A guest-facing message body for a given language and <see cref="MessageTemplateKind"/>,
/// e.g. the initial arrival request, a clarification follow-up, or a confirmation reply.</summary>
public sealed class MessageTemplate
{
    public required string LanguageCode { get; init; }
    public MessageTemplateKind Kind { get; init; } = MessageTemplateKind.Request;
    public required string Body { get; init; }
}
