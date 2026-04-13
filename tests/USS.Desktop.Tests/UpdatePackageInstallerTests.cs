using System.IO.Compression;
using System.Security.Cryptography;
using USS.Desktop.Updater;

namespace USS.Desktop.Tests;

public sealed class UpdatePackageInstallerTests
{
    [Fact]
    public async Task Install_ReplacesApplicationFiles()
    {
        using var tempDirectory = new TestDirectory();
        var appDirectory = Path.Combine(tempDirectory.Path, "app");
        var packageRoot = Path.Combine(tempDirectory.Path, "package");
        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(packageRoot);

        await File.WriteAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe"), "old app");
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "old.dll"), "old dll");

        await File.WriteAllTextAsync(Path.Combine(packageRoot, "USS.Desktop.App.exe"), "new app");
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "new.dll"), "new dll");
        Directory.CreateDirectory(Path.Combine(packageRoot, "toolsets"));
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "toolsets", "arduino-cli.exe"), "tool");

        UpdatePackageInstaller.Install(packageRoot, appDirectory, "USS.Desktop.App.exe");

        Assert.Equal("new app", await File.ReadAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe")));
        Assert.Equal("new dll", await File.ReadAllTextAsync(Path.Combine(appDirectory, "new.dll")));
        Assert.Equal("tool", await File.ReadAllTextAsync(Path.Combine(appDirectory, "toolsets", "arduino-cli.exe")));
        Assert.False(File.Exists(Path.Combine(appDirectory, "old.dll")));
        AssertNoBackupDirectories(appDirectory);
        AssertNoBackupDirectories(tempDirectory.Path);
    }

    [Fact]
    public async Task ExtractAndInstall_FromNestedReleaseZip_ReplacesApplicationFiles()
    {
        using var tempDirectory = new TestDirectory();
        var appDirectory = Path.Combine(tempDirectory.Path, "app");
        var packageContentRoot = Path.Combine(tempDirectory.Path, "package-content");
        var publishRoot = Path.Combine(packageContentRoot, "USS.Desktop-win-x64");
        var archivePath = Path.Combine(tempDirectory.Path, "release.zip");
        var stagingDirectory = Path.Combine(tempDirectory.Path, "staging");

        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(publishRoot);
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe"), "old app");
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "old.dll"), "old dll");
        await File.WriteAllTextAsync(Path.Combine(publishRoot, "USS.Desktop.App.exe"), "new app");
        await File.WriteAllTextAsync(Path.Combine(publishRoot, "new.dll"), "new dll");
        Directory.CreateDirectory(Path.Combine(publishRoot, "updater"));
        await File.WriteAllTextAsync(Path.Combine(publishRoot, "updater", "USS.Desktop.Updater.exe"), "updater");
        ZipFile.CreateFromDirectory(packageContentRoot, archivePath);

        UpdateArchiveExtractor.ExtractToDirectory(archivePath, stagingDirectory);
        UpdatePackageInstaller.Install(stagingDirectory, appDirectory, "USS.Desktop.App.exe");

        Assert.Equal("new app", await File.ReadAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe")));
        Assert.Equal("new dll", await File.ReadAllTextAsync(Path.Combine(appDirectory, "new.dll")));
        Assert.Equal("updater", await File.ReadAllTextAsync(Path.Combine(appDirectory, "updater", "USS.Desktop.Updater.exe")));
        Assert.False(File.Exists(Path.Combine(appDirectory, "old.dll")));
        AssertNoBackupDirectories(appDirectory);
    }

    [Fact]
    public async Task Install_RemovesStaleBackupDirectoryBeforeReplacement()
    {
        using var tempDirectory = new TestDirectory();
        var appDirectory = Path.Combine(tempDirectory.Path, "app");
        var packageRoot = Path.Combine(tempDirectory.Path, "package");
        var staleBackupDirectory = Path.Combine(appDirectory, ".USS.Desktop.UpdateBackup-stale");

        Directory.CreateDirectory(appDirectory);
        Directory.CreateDirectory(packageRoot);
        Directory.CreateDirectory(staleBackupDirectory);
        await File.WriteAllTextAsync(Path.Combine(staleBackupDirectory, "old.txt"), "stale");
        await File.WriteAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe"), "old app");
        await File.WriteAllTextAsync(Path.Combine(packageRoot, "USS.Desktop.App.exe"), "new app");

        UpdatePackageInstaller.Install(packageRoot, appDirectory, "USS.Desktop.App.exe");

        Assert.Equal("new app", await File.ReadAllTextAsync(Path.Combine(appDirectory, "USS.Desktop.App.exe")));
        AssertNoBackupDirectories(appDirectory);
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
        Assert.False(UpdateDigestVerifier.VerifyFile(filePath, null));
        Assert.False(UpdateDigestVerifier.VerifyFile(filePath, "not-a-sha256-digest"));
    }

    [Fact]
    public async Task ExtractToDirectory_RejectsUnsafeRelativePaths()
    {
        using var tempDirectory = new TestDirectory();
        var archivePath = Path.Combine(tempDirectory.Path, "malicious.zip");
        var stagingDirectory = Path.Combine(tempDirectory.Path, "staging");

        await using (var archiveStream = File.Create(archivePath))
        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../escape.txt");
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream);
            await writer.WriteAsync("escape");
        }

        var exception = Assert.Throws<InvalidDataException>(
            () => UpdateArchiveExtractor.ExtractToDirectory(archivePath, stagingDirectory));

        Assert.Contains("unsafe", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "escape.txt")));
    }

    [Fact]
    public void UpdaterOptions_IsValid_RequiresSha256Digest()
    {
        using var tempDirectory = new TestDirectory();
        var options = new UpdaterOptions(
            ProcessId: 123,
            AppDirectory: tempDirectory.Path,
            ExecutableName: "USS.Desktop.App.exe",
            DownloadUrl: new Uri("https://example.test/USS.Desktop-win-x64.zip"),
            Sha256Digest: null);

        Assert.False(options.IsValid(out var message));
        Assert.Contains("SHA-256", message, StringComparison.OrdinalIgnoreCase);
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

    private static void AssertNoBackupDirectories(string directory) =>
        Assert.Empty(Directory.EnumerateDirectories(directory, ".USS.Desktop.UpdateBackup-*"));
}
