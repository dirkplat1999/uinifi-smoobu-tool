using UnifiSmoobuTool.Infrastructure.Security;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class SecretProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTrips()
    {
        var protector = new SecretProtector();

        var protectedBytes = protector.Protect("my-api-key");
        var recovered = protector.Unprotect(protectedBytes);

        Assert.Equal("my-api-key", recovered);
    }

    [Fact]
    public void Protect_ProducesDifferentBytesThanPlainUtf8()
    {
        var protector = new SecretProtector();
        var protectedBytes = protector.Protect("my-api-key")!;

        Assert.DoesNotContain("my-api-key", System.Text.Encoding.UTF8.GetString(protectedBytes));
    }

    [Fact]
    public void Protect_ReturnsNull_ForNullOrEmptyInput()
    {
        var protector = new SecretProtector();
        Assert.Null(protector.Protect(null));
        Assert.Null(protector.Protect(""));
    }

    [Fact]
    public void Unprotect_ReturnsNull_ForNullOrEmptyInput()
    {
        var protector = new SecretProtector();
        Assert.Null(protector.Unprotect(null));
        Assert.Null(protector.Unprotect(Array.Empty<byte>()));
    }
}
