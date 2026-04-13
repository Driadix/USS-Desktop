using USS.Desktop.Application;
using USS.Desktop.Infrastructure;

namespace USS.Desktop.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_CapturesUtf8Output()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var tempDirectory = new TestDirectory();
        using var runner = new ProcessRunner();

        var request = new ProcessExecutionRequest(
            "powershell.exe",
            new[]
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); [Console]::Error.WriteLine('Ошибка'); [Console]::Out.WriteLine('Привет')"
            },
            tempDirectory.Path,
            new Dictionary<string, string?>());

        var result = await runner.RunAsync(request);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Привет", result.StandardOutput);
        Assert.Contains("Ошибка", result.StandardError);
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
