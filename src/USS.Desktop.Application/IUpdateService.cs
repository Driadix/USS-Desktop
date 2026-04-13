namespace USS.Desktop.Application;

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken = default);
}
