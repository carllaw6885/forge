using System.Security.Cryptography;
using System.Text;

namespace Forge.Storage;

/// <summary>
/// Time-limited, HMAC-signed access grants (ADR 14): the authorized private
/// access path. A token grants one blob id until expiry; there are no
/// permanent public URLs.
/// </summary>
public static class StorageAccessTokens
{
    public static string Create(string blobId, DateTimeOffset expiresAt, byte[] secret)
    {
        var payload = $"{blobId}|{expiresAt.ToUnixTimeSeconds()}";
        var signature = Convert.ToHexStringLower(
            HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(payload)));
        return $"{payload}|{signature}";
    }

    public static bool Validate(string token, string blobId, TimeProvider clock, byte[] secret)
    {
        var parts = token.Split('|');
        if (parts.Length != 3 || parts[0] != blobId || !long.TryParse(parts[1], out var expiresUnix))
        {
            return false;
        }

        var expected = Convert.ToHexStringLower(
            HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"{parts[0]}|{parts[1]}")));
        return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(parts[2]), Encoding.UTF8.GetBytes(expected))
            && DateTimeOffset.FromUnixTimeSeconds(expiresUnix) > clock.GetUtcNow();
    }
}
