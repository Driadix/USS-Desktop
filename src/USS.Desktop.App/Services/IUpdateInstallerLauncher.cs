using USS.Desktop.Application;

namespace USS.Desktop.App.Services;

public interface IUpdateInstallerLauncher
{
    void Launch(ApplicationRelease release);
}
