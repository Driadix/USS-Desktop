namespace USS.Desktop.Updater;

public sealed record UpdaterOptions(
    int? ProcessId,
    string? AppDirectory,
    string? ExecutableName,
    Uri? DownloadUrl,
    string? Sha256Digest)
{
    public static UpdaterOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Count; index++)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Count)
            {
                continue;
            }

            values[key[2..]] = args[++index];
        }

        values.TryGetValue("pid", out var processIdValue);
        values.TryGetValue("app-dir", out var appDirectory);
        values.TryGetValue("exe", out var executableName);
        values.TryGetValue("download-url", out var downloadUrlValue);
        values.TryGetValue("sha256", out var sha256Digest);

        _ = int.TryParse(processIdValue, out var processId);
        Uri.TryCreate(downloadUrlValue, UriKind.Absolute, out var downloadUrl);

        return new UpdaterOptions(
            processIdValue is null ? null : processId,
            appDirectory,
            executableName,
            downloadUrl,
            sha256Digest);
    }

    public bool IsValid(out string message)
    {
        if (string.IsNullOrWhiteSpace(AppDirectory) || !Directory.Exists(AppDirectory))
        {
            message = "Application directory is missing or does not exist.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ExecutableName))
        {
            message = "Executable name is missing.";
            return false;
        }

        if (DownloadUrl is null)
        {
            message = "Download URL is missing or invalid.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
