using USS.Desktop.Infrastructure;

namespace USS.Desktop.Tests;

public sealed class ArduinoCliToolsetResolverTests
{
    [Fact]
    public async Task ResolveAsync_FindsVersionedBundledToolsetUnderCurrentDirectory()
    {
        using var tempDirectory = new TestDirectory();
        var toolDirectory = Path.Combine(tempDirectory.Path, "toolsets", "arduino-cli-1.4.1-win64");
        Directory.CreateDirectory(toolDirectory);
        var executablePath = Path.Combine(toolDirectory, "arduino-cli.exe");
        await File.WriteAllTextAsync(executablePath, "stub");

        var originalOverrideRoot = Environment.GetEnvironmentVariable("USS_DESKTOP_TOOLSETS_ROOT");
        var originalLocalDataRoot = Environment.GetEnvironmentVariable("USS_DESKTOP_LOCAL_DATA_ROOT");
        var localDataRoot = Path.Combine(tempDirectory.Path, "local-data");
        try
        {
            Environment.SetEnvironmentVariable("USS_DESKTOP_TOOLSETS_ROOT", tempDirectory.Path);
            Environment.SetEnvironmentVariable("USS_DESKTOP_LOCAL_DATA_ROOT", localDataRoot);

            var resolver = new ArduinoCliToolsetResolver();
            var resolution = await resolver.ResolveAsync();

            Assert.True(resolution.IsAvailable);
            Assert.Equal(executablePath, resolution.ArduinoCliPath);
            Assert.Equal(Path.Combine(localDataRoot, "arduino-cli", "data"), resolution.ArduinoDataDirectory);
            Assert.Equal(Path.Combine(localDataRoot, "arduino-cli", "user"), resolution.ArduinoUserDirectory);
            Assert.True(Directory.Exists(resolution.ArduinoDataDirectory));
            Assert.True(Directory.Exists(resolution.ArduinoUserDirectory));
        }
        finally
        {
            Environment.SetEnvironmentVariable("USS_DESKTOP_TOOLSETS_ROOT", originalOverrideRoot);
            Environment.SetEnvironmentVariable("USS_DESKTOP_LOCAL_DATA_ROOT", originalLocalDataRoot);
        }
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
