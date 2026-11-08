using System.Security.Cryptography;

namespace OnlineShop.IntegrationTests.TestSupport;

/// <summary>RFC 6238 TOTP (SHA-1, 30 s, 6 digits) — what Google/Microsoft Authenticator compute from the shared key.</summary>
public static class Totp
{
    public static string Code(string sharedKey, DateTimeOffset? at = null)
    {
        var key = Base32Decode(sharedKey.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant());
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        var message = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(message);

        // SHA-1 is what RFC 6238 and every authenticator app use (HMAC-SHA1 is not broken like plain SHA-1).
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(key, message);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input.TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. output];
    }
}
