using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Services;

/// <summary>Canonical default template bodies (and, for email use, subjects) for the languages the
/// app ships built-in translations for (English, Dutch, German, French). Used both to seed a fresh
/// database (<see cref="Infrastructure.Persistence.SqliteConnectionFactory"/>) and to prefill a
/// sensible starting body/subject when adding a template for one of these languages via the UI, so
/// the two stay in sync instead of drifting apart.</summary>
public static class DefaultMessageTemplates
{
    public static IReadOnlyList<MessageTemplate> All { get; } = new[]
    {
        new MessageTemplate
        {
            LanguageCode = "en", Kind = MessageTemplateKind.Request,
            Subject = "Your arrival at {{apartment_name}}",
            Body = "Hi {{guest_first_name}}, welcome to {{apartment_name}}! Could you please send us your license plate number and a 4-digit PIN code you'd like to use, before your arrival on {{arrival_date}}? Thank you!",
        },
        new MessageTemplate
        {
            LanguageCode = "en", Kind = MessageTemplateKind.Clarification,
            Subject = "Quick follow-up - {{apartment_name}}",
            Body = "Hi {{guest_first_name}}, sorry, we couldn't quite make out your license plate and PIN from your last message. Could you send them again, clearly, e.g. \"Plate: AB-123-C, PIN: 4821\"?",
        },
        new MessageTemplate
        {
            LanguageCode = "en", Kind = MessageTemplateKind.Confirmation,
            Subject = "You're all set - {{apartment_name}}",
            Body = "Thanks {{guest_first_name}}, we've got your license plate and PIN - you're all set for your arrival on {{arrival_date}}!",
        },

        new MessageTemplate
        {
            LanguageCode = "nl", Kind = MessageTemplateKind.Request,
            Subject = "Uw aankomst bij {{apartment_name}}",
            Body = "Hallo {{guest_first_name}}, welkom bij {{apartment_name}}! Zou u ons vóór uw aankomst op {{arrival_date}} uw kenteken en een 4-cijferige pincode willen doorgeven? Alvast bedankt!",
        },
        new MessageTemplate
        {
            LanguageCode = "nl", Kind = MessageTemplateKind.Clarification,
            Subject = "Even navragen - {{apartment_name}}",
            Body = "Hallo {{guest_first_name}}, we konden het kenteken en de pincode uit uw vorige bericht niet goed lezen. Zou u ze nogmaals duidelijk willen doorgeven, bijvoorbeeld \"Kenteken: AB-123-C, pincode: 4821\"?",
        },
        new MessageTemplate
        {
            LanguageCode = "nl", Kind = MessageTemplateKind.Confirmation,
            Subject = "U bent helemaal klaar - {{apartment_name}}",
            Body = "Bedankt {{guest_first_name}}, we hebben uw kenteken en pincode ontvangen - u bent helemaal klaar voor uw aankomst op {{arrival_date}}!",
        },

        new MessageTemplate
        {
            LanguageCode = "de", Kind = MessageTemplateKind.Request,
            Subject = "Ihre Ankunft bei {{apartment_name}}",
            Body = "Hallo {{guest_first_name}}, willkommen bei {{apartment_name}}! Könnten Sie uns vor Ihrer Ankunft am {{arrival_date}} bitte Ihr Kennzeichen und einen 4-stelligen PIN-Code mitteilen? Vielen Dank!",
        },
        new MessageTemplate
        {
            LanguageCode = "de", Kind = MessageTemplateKind.Clarification,
            Subject = "Kurze Rückfrage - {{apartment_name}}",
            Body = "Hallo {{guest_first_name}}, wir konnten Ihr Kennzeichen und Ihren PIN-Code aus Ihrer letzten Nachricht leider nicht eindeutig entnehmen. Könnten Sie beides bitte noch einmal klar mitteilen, z. B. \"Kennzeichen: AB-123-C, PIN: 4821\"?",
        },
        new MessageTemplate
        {
            LanguageCode = "de", Kind = MessageTemplateKind.Confirmation,
            Subject = "Alles bereit - {{apartment_name}}",
            Body = "Danke {{guest_first_name}}, wir haben Ihr Kennzeichen und Ihren PIN-Code erhalten - für Ihre Ankunft am {{arrival_date}} ist alles bereit!",
        },

        new MessageTemplate
        {
            LanguageCode = "fr", Kind = MessageTemplateKind.Request,
            Subject = "Votre arrivée à {{apartment_name}}",
            Body = "Bonjour {{guest_first_name}}, bienvenue à {{apartment_name}} ! Pourriez-vous nous communiquer votre plaque d'immatriculation et un code PIN à 4 chiffres avant votre arrivée le {{arrival_date}} ? Merci !",
        },
        new MessageTemplate
        {
            LanguageCode = "fr", Kind = MessageTemplateKind.Clarification,
            Subject = "Petite précision - {{apartment_name}}",
            Body = "Bonjour {{guest_first_name}}, nous n'avons pas pu lire clairement votre plaque d'immatriculation et votre code PIN dans votre dernier message. Pourriez-vous nous les renvoyer clairement, par exemple \"Plaque : AB-123-C, PIN : 4821\" ?",
        },
        new MessageTemplate
        {
            LanguageCode = "fr", Kind = MessageTemplateKind.Confirmation,
            Subject = "Tout est prêt - {{apartment_name}}",
            Body = "Merci {{guest_first_name}}, nous avons bien reçu votre plaque d'immatriculation et votre code PIN - tout est prêt pour votre arrivée le {{arrival_date}} !",
        },
    };

    /// <summary>Returns the canonical body for this language+kind, or null when the app has no
    /// built-in translation for that language (e.g. a custom/other language code).</summary>
    public static string? TryGetBody(string languageCode, MessageTemplateKind kind) =>
        Find(languageCode, kind)?.Body;

    /// <summary>Returns the canonical email subject for this language+kind, or null when the app
    /// has no built-in translation for that language.</summary>
    public static string? TryGetSubject(string languageCode, MessageTemplateKind kind) =>
        Find(languageCode, kind)?.Subject;

    private static MessageTemplate? Find(string languageCode, MessageTemplateKind kind) =>
        All.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase) && t.Kind == kind);
}
