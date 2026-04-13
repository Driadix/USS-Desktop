using System.Security.Cryptography;

namespace USS.Desktop.Updater;

public static class UpdateDigestVerifier
{
    public static bool VerifyFile(string filePath, string? expectedSha256Digest)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256Digest))
        {
            return false;
        }

        var expectedDigest = NormalizeSha256Digest(expectedSha256Digest);
        if (expectedDigest is null)
        {
            return false;
        }

        using var stream = File.OpenRead(filePath);
        var actualDigest = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeSha256Digest(string value)
    {
        var trimmedValue = value.Trim();
        const string prefix = "sha256:";
        if (trimmedValue.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmedValue = trimmedValue[prefix.Length..];
        }

        return trimmedValue.Length == 64 && trimmedValue.All(Uri.IsHexDigit)
            ? trimmedValue.ToLowerInvariant()
            : null;
    }
}
