using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class GuestReplyParserTests
{
    [Fact]
    public void Parse_ExtractsPinAndPlate_WhenReplyIsUnambiguous()
    {
        var result = GuestReplyParser.Parse("Hi! Our plate is AB-123-C and the PIN is 4821, thanks!");

        Assert.Equal("4821", result.PinCode);
        Assert.Equal("AB-123-C", result.RawLicensePlate);
        Assert.True(result.IsConfident);
    }

    [Fact]
    public void Parse_IsNotConfident_WhenNoPinFound()
    {
        var result = GuestReplyParser.Parse("Our plate is AB-123-C");

        Assert.Null(result.PinCode);
        Assert.False(result.IsConfident);
    }

    [Fact]
    public void Parse_IsNotConfident_WhenMultiplePinLikeNumbersPresent()
    {
        var result = GuestReplyParser.Parse("Plate AB123C, PIN 4821, arriving around 1800");

        Assert.False(result.IsConfident);
    }

    [Fact]
    public void Parse_DoesNotTreatPinAsPlateCandidate()
    {
        var result = GuestReplyParser.Parse("4821");

        Assert.Equal("4821", result.PinCode);
        Assert.Null(result.RawLicensePlate);
        Assert.False(result.IsConfident);
    }
}
