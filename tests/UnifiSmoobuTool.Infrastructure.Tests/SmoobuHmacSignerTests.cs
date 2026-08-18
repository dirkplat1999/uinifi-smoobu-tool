using UnifiSmoobuTool.Infrastructure.Smoobu;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class SmoobuHmacSignerTests
{
    [Fact]
    public void Sign_MatchesIndependentlyComputedGoldenVector()
    {
        // Cross-checked against an independent Python (hashlib/hmac) computation of the same
        // canonical string, so this pins down the exact byte-for-byte behavior of this class
        // even though the canonical string format itself is unverified against Smoobu's live API.
        var queryParams = new Dictionary<string, string> { ["to"] = "2026-04-10", ["from"] = "2026-04-01" };
        var now = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var nonce = "11111111-1111-1111-1111-111111111111";

        var result = SmoobuHmacSigner.Sign(
            "GET", "/api/reservations", queryParams, body: "",
            apiKey: "usr_live_test123", apiSecret: "test-secret-value", now, nonce);

        Assert.Equal("2026-04-01T12:00:00Z", result.Timestamp);
        Assert.Equal(nonce, result.Nonce);
        Assert.Equal("an6mAaA65CP6y9xX+9yoK8t4mT1+lF7qczAX1ujHdRA=", result.Signature);
    }

    [Fact]
    public void BuildCanonicalQueryString_SortsKeysAlphabetically_RegardlessOfInputOrder()
    {
        var queryParams = new Dictionary<string, string> { ["to"] = "2026-04-10", ["from"] = "2026-04-01" };

        var result = SmoobuHmacSigner.BuildCanonicalQueryString(queryParams);

        Assert.Equal("from=2026-04-01&to=2026-04-10", result);
    }

    [Fact]
    public void BuildCanonicalQueryString_ReturnsEmptyString_WhenNoParams()
    {
        Assert.Equal("", SmoobuHmacSigner.BuildCanonicalQueryString(null));
        Assert.Equal("", SmoobuHmacSigner.BuildCanonicalQueryString(new Dictionary<string, string>()));
    }

    [Fact]
    public void Sign_ProducesDifferentSignatures_ForDifferentSecrets()
    {
        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString();

        var a = SmoobuHmacSigner.Sign("GET", "/api/apartments", null, "", "key", "secret-a", now, nonce);
        var b = SmoobuHmacSigner.Sign("GET", "/api/apartments", null, "", "key", "secret-b", now, nonce);

        Assert.NotEqual(a.Signature, b.Signature);
    }

    [Fact]
    public void Sign_ProducesDifferentSignatures_ForDifferentBodies()
    {
        var now = DateTimeOffset.UtcNow;
        var nonce = Guid.NewGuid().ToString();

        var a = SmoobuHmacSigner.Sign("POST", "/api/reservations/1/messages/send-message-to-guest", null, "{\"message\":\"hi\"}", "key", "secret", now, nonce);
        var b = SmoobuHmacSigner.Sign("POST", "/api/reservations/1/messages/send-message-to-guest", null, "{\"message\":\"bye\"}", "key", "secret", now, nonce);

        Assert.NotEqual(a.Signature, b.Signature);
    }
}
