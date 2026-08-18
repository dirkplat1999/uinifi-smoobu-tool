using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class TemplateRendererTests
{
    [Fact]
    public void Render_SubstitutesPlaceholders_CaseInsensitively()
    {
        var result = TemplateRenderer.Render(
            "Hi {{Guest_First_Name}}, welcome to {{apartment_name}}!",
            new Dictionary<string, string>
            {
                ["guest_first_name"] = "Alex",
                ["apartment_name"] = "Canal View",
            });

        Assert.Equal("Hi Alex, welcome to Canal View!", result);
    }

    [Fact]
    public void SelectTemplate_PrefersGuestLanguage_WhenAvailable()
    {
        var templates = new[]
        {
            new MessageTemplate { LanguageCode = "en", Body = "english" },
            new MessageTemplate { LanguageCode = "nl", Body = "dutch" },
        };

        var selected = TemplateRenderer.SelectTemplate(templates, MessageTemplateKind.Request, "nl", "en");

        Assert.Equal("dutch", selected.Body);
    }

    [Fact]
    public void SelectTemplate_FallsBackToDefaultLanguage_WhenGuestLanguageMissing()
    {
        var templates = new[]
        {
            new MessageTemplate { LanguageCode = "en", Body = "english" },
            new MessageTemplate { LanguageCode = "nl", Body = "dutch" },
        };

        var selected = TemplateRenderer.SelectTemplate(templates, MessageTemplateKind.Request, "fr", "en");

        Assert.Equal("english", selected.Body);
    }

    [Fact]
    public void SelectTemplate_Throws_WhenNoTemplatesConfigured()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TemplateRenderer.SelectTemplate(Array.Empty<MessageTemplate>(), MessageTemplateKind.Request, "en", "en"));
    }

    [Fact]
    public void SelectTemplate_OnlyConsidersTemplatesOfTheRequestedKind()
    {
        var templates = new[]
        {
            new MessageTemplate { LanguageCode = "en", Kind = MessageTemplateKind.Request, Body = "request" },
            new MessageTemplate { LanguageCode = "en", Kind = MessageTemplateKind.Confirmation, Body = "confirmation" },
        };

        var selected = TemplateRenderer.SelectTemplate(templates, MessageTemplateKind.Confirmation, "en", "en");

        Assert.Equal("confirmation", selected.Body);
    }

    [Fact]
    public void SelectTemplate_Throws_WhenNoTemplatesOfTheRequestedKindExist()
    {
        var templates = new[] { new MessageTemplate { LanguageCode = "en", Kind = MessageTemplateKind.Request, Body = "request" } };

        Assert.Throws<InvalidOperationException>(() =>
            TemplateRenderer.SelectTemplate(templates, MessageTemplateKind.Clarification, "en", "en"));
    }
}
