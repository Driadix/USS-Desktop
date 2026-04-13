using System.Security.Cryptography;
using USS.Desktop.Updater;

namespace USS.Desktop.Tests;

public sealed class UpdatePackageInstallerTests
{
    [Fact]
    public async Task Install_ReplacesApplicationFilesAndPreservesLocalStateDirectories()
    {
        using var tempDirectory = new TestDirectory();
        var appDirectory = Path.Combine(tempDirectory.Path, "app");
        var packageRoot = Path.Combine(tempDirectory.Path, "package");
        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(packageRoot);

        await File.WriteAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe"), "old app");
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "old.dll"), "old dll");
        Directory.CreateDirectory(Path.Combine(appDirectory, "uss-data"));
        Directory.CreateDirectory(Path.Combine(appDirectory, "app-logs"));
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "uss-data", "app-settings.json"), "settings");
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "app-logs", "startup.log"), "log");

        await File.WriteAllTextAsync(Path.Combine(packageRoot, "USS.Desktop.App.exe"), "new app");
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "new.dll"), "new dll");
        Directory.CreateDirectory(Path.Combine(packageRoot, "toolsets"));
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "toolsets", "arduino-cli.exe"), "tool");

        UpdatePackageInstaller.Install(packageRoot, appDirectory, "USS.Desktop.App.exe");

        Assert.Equal("new app", await File.ReadAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe")));
        Assert.Equal("new dll", await File.ReadAllTextAsync(Path.Combine(appDirectory, "new.dll")));
        Assert.Equal("tool", await File.ReadAllTextAsync(Path.Combine(appDirectory, "toolsets", "arduino-cli.exe")));
        Assert.False(File.Exists(Path.Combine(appDirectory, "old.dll")));
        Assert.Equal("settings", await File.ReadAllTextAsync(Path.Combine(appDirectory, "uss-data", "app-settings.json")));
        Assert.Equal("log", await File.ReadAllTextAsync(Path.Combine(appDirectory, "app-logs", "startup.log")));
    }

    [Fact]
    public async Task VerifyFile_ReturnsFalseForDigestMismatch()
    {
        using var tempDirectory = new TestDirectory();
        var filePath = Path.Combine(tempDirectory.Path, "update.zip");
        await File.WriteAllTextAsync(filePath, "package");

        await using var stream = File.OpenRead(filePath);
        var actualDigest = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();

        Assert.True(UpdateDigestVerifier.VerifyFile(filePath, $"sha256:{actualDigest}"));
        Assert.False(UpdateDigestVerifier.VerifyFile(filePath, $"sha256:{new string('0', 64)}"));
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "uss-desktop-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
