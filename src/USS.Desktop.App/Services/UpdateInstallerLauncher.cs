using System.Diagnostics;
using System.IO;
using USS.Desktop.Application;

namespace USS.Desktop.App.Services;

public sealed class UpdateInstallerLauncher : IUpdateInstallerLauncher
{
    private const string UpdaterExecutableName = "USS.Desktop.Updater.exe";

    public void Launch(ApplicationRelease release)
    {
        var sourceDirectory = Path.Combine(AppContext.BaseDirectory, "updater");
        var sourceExecutablePath = Path.Combine(sourceDirectory, UpdaterExecutableName);
        if (!File.Exists(sourceExecutablePath))
        {
            throw new FileNotFoundException("Updater executable was not found. Publish the app before using self-update.", sourceExecutablePath);
        }

        var launchDirectory = Path.Combine(Path.GetTempPath(), "USS.Desktop.Updater", Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceDirectory, launchDirectory);

        var updaterExecutablePath = Path.Combine(launchDirectory, UpdaterExecutableName);
        var applicationDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var applicationExecutable = Path.GetFileName(Environment.ProcessPath);
        if (string.IsNullOrWhiteSpace(applicationExecutable))
        {
            applicationExecutable = "USS.Desktop.App.exe";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterExecutablePath,
            WorkingDirectory = launchDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--app-dir");
        startInfo.ArgumentList.Add(applicationDirectory);
        startInfo.ArgumentList.Add("--exe");
        startInfo.ArgumentList.Add(applicationExecutable);
        startInfo.ArgumentList.Add("--download-url");
        startInfo.ArgumentList.Add(release.DownloadUrl.AbsoluteUri);
        startInfo.ArgumentList.Add("--release-url");
        startInfo.ArgumentList.Add(release.ReleasePageUrl.AbsoluteUri);

        if (string.IsNullOrWhiteSpace(release.Sha256Digest))
        {
            throw new InvalidOperationException("Update package SHA-256 digest is missing.");
        }

        startInfo.ArgumentList.Add("--sha256");
        startInfo.ArgumentList.Add(release.Sha256Digest);

        Process.Start(startInfo);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
            {
                Directory.CreateDirectory(destinationDirectoryPath);
            }

            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
