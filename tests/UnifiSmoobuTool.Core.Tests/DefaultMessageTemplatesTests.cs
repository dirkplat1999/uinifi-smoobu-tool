using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class DefaultMessageTemplatesTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("nl")]
    [InlineData("de")]
    [InlineData("fr")]
    public void All_HasAllThreeKinds_ForEachBuiltInLanguage(string languageCode)
    {
        foreach (var kind in Enum.GetValues<MessageTemplateKind>())
        {
            var body = DefaultMessageTemplates.TryGetBody(languageCode, kind);
            Assert.False(string.IsNullOrWhiteSpace(body));

            var subject = DefaultMessageTemplates.TryGetSubject(languageCode, kind);
            Assert.False(string.IsNullOrWhiteSpace(subject));
        }
    }

    [Fact]
    public void TryGetSubject_ReturnsNull_ForUnknownLanguage()
    {
        Assert.Null(DefaultMessageTemplates.TryGetSubject("es", MessageTemplateKind.Request));
    }

    [Fact]
    public void TryGetBody_ReturnsLanguageSpecificText_NotEnglishFallback()
    {
        var dutch = DefaultMessageTemplates.TryGetBody("nl", MessageTemplateKind.Request);
        var english = DefaultMessageTemplates.TryGetBody("en", MessageTemplateKind.Request);

        Assert.NotEqual(english, dutch);
        Assert.Contains("kenteken", dutch, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetBody_ReturnsNull_ForUnknownLanguage()
    {
        Assert.Null(DefaultMessageTemplates.TryGetBody("es", MessageTemplateKind.Request));
    }

    [Fact]
    public void All_HasExactlyTwelveEntries()
    {
        Assert.Equal(12, DefaultMessageTemplates.All.Count);
    }
}
