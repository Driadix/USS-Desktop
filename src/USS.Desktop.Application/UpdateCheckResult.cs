namespace USS.Desktop.Application;

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    ApplicationRelease? Release,
    string Message)
{
    public static UpdateCheckResult UpToDate(Version currentVersion, string message) =>
        new(UpdateCheckStatus.UpToDate, currentVersion, null, message);

    public static UpdateCheckResult UpdateAvailable(Version currentVersion, ApplicationRelease release, string message) =>
        new(UpdateCheckStatus.UpdateAvailable, currentVersion, release, message);

    public static UpdateCheckResult Failed(Version currentVersion, string message) =>
        new(UpdateCheckStatus.Failed, currentVersion, null, message);
}
