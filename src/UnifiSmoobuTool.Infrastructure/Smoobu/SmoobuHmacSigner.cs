using System.Security.Cryptography;
using System.Text;

namespace UnifiSmoobuTool.Infrastructure.Smoobu;

/// <summary>
/// Computes Smoobu's HMAC-SHA256 request signature (the API key + secret authentication scheme
/// that superseded the legacy single "Api-Key" header). Based on Smoobu's published HMAC
/// authentication guide - not yet exercised against a live account, since no working secret was
/// available while building this. If requests come back 401 once a real secret is configured,
/// check the Log Viewer for the failure and adjust the canonical string format here; this class
/// is deliberately isolated and unit-tested so that's a small, safe change.
///
/// Canonical string: "{METHOD}\n{PATH}\n{CANONICAL_QUERY}\n{TIMESTAMP}\n{NONCE}\n{BODY_SHA256_HEX}\n{API_KEY}"
///   - METHOD: HTTP method, uppercase (e.g. "GET", "POST")
///   - PATH: request path only, e.g. "/api/reservations" (no host, no query string)
///   - CANONICAL_QUERY: query parameters sorted by key (ordinal), formatted "key=value&key2=value2"
///     with both keys and values percent-encoded; empty string when there are no query parameters
///   - TIMESTAMP: ISO 8601 UTC, e.g. "2026-08-18T12:00:00Z"
///   - NONCE: a fresh UUID v4 per request
///   - BODY_SHA256_HEX: lowercase hex SHA-256 digest of the UTF-8 request body (or of the empty
///     string, which hashes to "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
///     when there is no body)
///   - API_KEY: the account's API key/label
/// X-Signature is Base64(HMAC-SHA256(secret, canonical string)).
/// </summary>
internal static class SmoobuHmacSigner
{
    public sealed record SignatureResult(string Timestamp, string Nonce, string Signature);

    public static SignatureResult Sign(
        string method,
        string path,
        IReadOnlyDictionary<string, string>? queryParams,
        string body,
        string apiKey,
        string apiSecret,
        DateTimeOffset now,
        string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiSecret);

        var timestamp = now.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var canonicalQuery = BuildCanonicalQueryString(queryParams);
        var bodyHash = Sha256Hex(body);

        var canonical = string.Join(
            "\n", method.ToUpperInvariant(), path, canonicalQuery, timestamp, nonce, bodyHash, apiKey);

        var signature = HmacSha256Base64(canonical, apiSecret);

        return new SignatureResult(timestamp, nonce, signature);
    }

    public static SignatureResult Sign(
        string method, string path, IReadOnlyDictionary<string, string>? queryParams, string body,
        string apiKey, string apiSecret)
        => Sign(method, path, queryParams, body, apiKey, apiSecret, DateTimeOffset.UtcNow, Guid.NewGuid().ToString());

    public static string BuildCanonicalQueryString(IReadOnlyDictionary<string, string>? queryParams)
    {
        if (queryParams is null || queryParams.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "&",
            queryParams
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HmacSha256Base64(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash);
    }
}
