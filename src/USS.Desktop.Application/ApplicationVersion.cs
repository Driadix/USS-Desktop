using System.Text.RegularExpressions;

namespace USS.Desktop.Application;

public static partial class ApplicationVersion
{
    public static Version Unknown { get; } = new(0, 0, 0, 0);

    public static bool TryParseTag(string? value, out Version version)
    {
        version = Unknown;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = ReleaseTagPattern().Match(value.Trim());
        return match.Success && TryParseVersion(match.Groups["version"].Value, out version);
    }

    public static bool TryParseVersion(string? value, out Version version)
    {
        version = Unknown;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalizedValue = value.Trim();
        var buildMetadataIndex = normalizedValue.IndexOf('+', StringComparison.Ordinal);
        if (buildMetadataIndex >= 0)
        {
            normalizedValue = normalizedValue[..buildMetadataIndex];
        }

        var prereleaseIndex = normalizedValue.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            normalizedValue = normalizedValue[..prereleaseIndex];
        }

        return Version.TryParse(normalizedValue, out var parsedVersion) && TryNormalize(parsedVersion, out version);
    }

    public static Version Normalize(Version version) =>
        new(
            Math.Max(version.Major, 0),
            Math.Max(version.Minor, 0),
            Math.Max(version.Build, 0),
            Math.Max(version.Revision, 0));

    public static string FormatForDisplay(Version version)
    {
        var normalizedVersion = Normalize(version);
        return $"v{normalizedVersion.Major}.{normalizedVersion.Minor}.{normalizedVersion.Build}";
    }

    private static bool TryNormalize(Version parsedVersion, out Version version)
    {
        version = Unknown;
        if (parsedVersion.Major < 0 || parsedVersion.Minor < 0)
        {
            return false;
        }

        version = Normalize(parsedVersion);
        return true;
    }

    [GeneratedRegex("^v?(?<version>\\d+\\.\\d+\\.\\d+(?:\\.\\d+)?)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReleaseTagPattern();
}
