using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Messaging;

/// <summary>
/// Manual verification of QStash's "Upstash-Signature" header — there is no official .NET SDK.
/// Structure confirmed by hand against a running qstash-cli dev server: a compact JWT, alg HS256,
/// HMAC key = the raw UTF8 bytes of the signing key string (e.g. "sig_..."), claims
/// iss="Upstash", sub=<exact destination URL>, exp/nbf as unix-seconds longs, and a `body` claim
/// holding base64url (padding sometimes retained, sometimes not — observed both) of the SHA-256
/// hash of the exact raw request body bytes. Confirmed by capturing real deliveries: a first
/// sample happened to contain no -/_ characters and looked like standard base64, which masked
/// this until a second capture's body hash contained '-'/'_' and failed to decode as base64.
/// </summary>
public static class QStashSignatureVerifier
{
    public static bool Verify(string signatureHeader, byte[] rawBody, string expectedUrl, QStashOptions options) =>
        TryVerifyWithKey(signatureHeader, rawBody, expectedUrl, options.CurrentSigningKey) ||
        TryVerifyWithKey(signatureHeader, rawBody, expectedUrl, options.NextSigningKey);

    private static bool TryVerifyWithKey(string token, byte[] rawBody, string expectedUrl, string signingKey)
    {
        if (string.IsNullOrEmpty(signingKey))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var expectedSignature = Base64UrlDecode(parts[2]);
            var actualSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), signingInput);

            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
            {
                return false;
            }

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var root = payload.RootElement;

            if (root.GetProperty("iss").GetString() != "Upstash")
            {
                return false;
            }

            if (root.GetProperty("sub").GetString() != expectedUrl)
            {
                return false;
            }

            // Plain long comparisons (not DateTimeOffset conversions) deliberately: the dev
            // server emits nbf as Go's zero-time sentinel (-62135596800), which sits right on
            // DateTimeOffset's supported boundary — safer to just compare unix seconds directly.
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (root.TryGetProperty("exp", out var exp) && now >= exp.GetInt64())
            {
                return false;
            }

            if (root.TryGetProperty("nbf", out var nbf) && now < nbf.GetInt64())
            {
                return false;
            }

            var expectedBodyHash = Base64UrlDecode(root.GetProperty("body").GetString()!);
            var actualBodyHash = SHA256.HashData(rawBody);
            return CryptographicOperations.FixedTimeEquals(expectedBodyHash, actualBodyHash);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Malformed token, unexpected claim shape, or bad base64 — treat as verification failure.
            return false;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
        return Convert.FromBase64String(s);
    }
}
