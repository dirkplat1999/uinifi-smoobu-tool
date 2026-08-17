using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class PlateNormalizerTests
{
    [Theory]
    [InlineData("AB-123-C", null, "AB123C")]
    [InlineData("ab 123 c", null, "AB123C")]
    [InlineData("NL-AB-123-C", new[] { "NL" }, "AB123C")]
    [InlineData("D AB123C", new[] { "NL", "D" }, "AB123C")]
    [InlineData("GB-123-ABC", new[] { "GB" }, "123ABC")]
    public void Normalize_StripsPunctuationSpacesAndDelimitedCountryPrefix(string input, string[]? prefixes, string expected)
    {
        var result = PlateNormalizer.Normalize(input, prefixes);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_DoesNotStripPrefix_WhenNotFollowedByADelimiter()
    {
        // "AB123C" starts with the letter "A" (a valid country prefix), but with no delimiter after
        // it there's no real signal it's a land indicator rather than the plate's own first letter.
        var result = PlateNormalizer.Normalize("AB123C", new[] { "A" });
        Assert.Equal("AB123C", result);
    }

    [Fact]
    public void Normalize_DoesNotStripPrefixIfItWouldConsumeTheWholePlate()
    {
        var result = PlateNormalizer.Normalize("NL", new[] { "NL" });
        Assert.Equal("NL", result);
    }

    [Fact]
    public void Normalize_PrefersLongerPrefixMatch()
    {
        var result = PlateNormalizer.Normalize("GB-123-ABC", new[] { "G", "GB" });
        Assert.Equal("123ABC", result);
    }
}
