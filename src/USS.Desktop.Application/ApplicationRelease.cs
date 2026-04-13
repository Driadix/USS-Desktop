namespace USS.Desktop.Application;

public sealed record ApplicationRelease(
    string TagName,
    Version Version,
    string AssetName,
    Uri DownloadUrl,
    Uri ReleasePageUrl,
    string? Sha256Digest);
