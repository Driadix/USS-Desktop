using USS.Desktop.Application;
using USS.Desktop.Domain;
using USS.Desktop.Infrastructure;

namespace USS.Desktop.Tests;

public sealed class ArduinoCliWorkflowServiceTests
{
    [Fact]
    public async Task CompileAsync_BuildsExpectedArduinoCliCommand()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "compile ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.CompileAsync(project);

        Assert.True(result.Success);
        Assert.NotNull(processRunner.LastRequest);
        Assert.Equal("arduino-cli.exe", Path.GetFileName(processRunner.LastRequest!.FileName));
        Assert.Equal("compile", processRunner.LastRequest.Arguments[0]);
        Assert.Equal(project.Files.ProjectDirectory, processRunner.LastRequest.Arguments[1]);
        Assert.Contains("--profile", processRunner.LastRequest.Arguments);
        Assert.Contains("--build-path", processRunner.LastRequest.Arguments);
        Assert.Contains("--output-dir", processRunner.LastRequest.Arguments);
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "data"), processRunner.LastRequest.EnvironmentVariables["ARDUINO_DIRECTORIES_DATA"]);
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "user"), processRunner.LastRequest.EnvironmentVariables["ARDUINO_DIRECTORIES_USER"]);
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "build", "work", "uss.lock")));
        Assert.True(File.Exists(result.LogFilePath));
    }

    [Fact]
    public async Task CompileAsync_ReportsLiveArduinoCliOutputWithPrefixes()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(
            new ProcessExecutionResult(0, "compile ok", "warning text", TimeSpan.FromSeconds(1)),
            request =>
            {
                request.OutputProgress?.Report(new ProcessOutputLine(ProcessOutputKind.StandardOutput, "Detecting libraries"));
                request.OutputProgress?.Report(new ProcessOutputLine(ProcessOutputKind.StandardError, "warning: deprecated"));
            });

        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var progressLines = new List<string>();
        var project = CreateManagedProject(tempDirectory.Path);

        var result = await workflowService.CompileAsync(project, new Progress<string>(progressLines.Add));

        Assert.True(result.Success);
        Assert.Contains(progressLines, line => line.StartsWith("CLI CMD | ", StringComparison.Ordinal));
        Assert.Contains("CLI OUT | Detecting libraries", progressLines);
        Assert.Contains("CLI ERR | warning: deprecated", progressLines);
        Assert.Contains("CLI EXIT | code 0", progressLines);
    }

    [Fact]
    public async Task UploadAsync_WithMultiplePortsAndAutoSelection_FailsWithoutRunningCli()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "upload ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(new[]
            {
                new ConnectedSerialPort("COM3", "COM3"),
                new ConnectedSerialPort("COM4", "COM4")
            }));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.UploadAsync(project, portOverride: null);

        Assert.False(result.Success);
        Assert.Contains("serial port", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Null(processRunner.LastRequest);
    }

    [Fact]
    public async Task UploadAsync_WithExplicitPort_UsesInputDirectoryAndVerifyFlag()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "upload ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.UploadAsync(project, "COM7");

        Assert.True(result.Success);
        Assert.NotNull(processRunner.LastRequest);
        Assert.Equal("upload", processRunner.LastRequest!.Arguments[0]);
        Assert.Contains("--input-dir", processRunner.LastRequest.Arguments);
        Assert.Contains("--port", processRunner.LastRequest.Arguments);
        Assert.Contains("COM7", processRunner.LastRequest.Arguments);
        Assert.Contains("--verify", processRunner.LastRequest.Arguments);
    }

    private static ProjectContext CreateManagedProject(string rootPath)
    {
        Directory.CreateDirectory(rootPath);

        return new ProjectContext(
            new ProjectFiles(rootPath, Path.Combine(rootPath, "uss.yaml"), Path.Combine(rootPath, "sketch.yaml"), Path.Combine(rootPath, "radio_test_v2.ino")),
            ProjectDiscoveryKind.ManagedProject,
            new UssProjectConfiguration(
                1,
                new ProjectMetadata("Radio Test", "arduino", ProjectFamily.Esp32, "main"),
                new ArtifactLayout("build/out", "build/logs", "build/work"),
                new UploadPreferences("auto", true)),
            new SketchConfiguration(
                "main",
                new Dictionary<string, SketchProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["main"] = new(
                        "main",
                        null,
                        "esp32:esp32:lilygo_t_display_s3",
                        new[] { new SketchPlatformReference("esp32:esp32", "3.3.7", "https://espressif.github.io/arduino-esp32/package_esp32_index.json") },
                        new[] { new SketchLibraryReference("TFT_eSPI", "2.5.43") },
                        null,
                        "serial")
                }),
            "main",
            new SketchProfile(
                "main",
                null,
                "esp32:esp32:lilygo_t_display_s3",
                new[] { new SketchPlatformReference("esp32:esp32", "3.3.7", "https://espressif.github.io/arduino-esp32/package_esp32_index.json") },
                new[] { new SketchLibraryReference("TFT_eSPI", "2.5.43") },
                null,
                "serial"),
            ProjectFamily.Esp32,
            Array.Empty<ProjectValidationIssue>());
    }

    private sealed class StubToolsetResolver : IToolsetResolver
    {
        private readonly ToolsetResolution _resolution;

        public StubToolsetResolver(string rootPath)
        {
            var cliPath = Path.Combine(rootPath, "toolsets", "arduino-cli.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(cliPath)!);
            File.WriteAllText(cliPath, "stub");

            var dataDirectory = Path.Combine(rootPath, "arduino-data", "data");
            var userDirectory = Path.Combine(rootPath, "arduino-data", "user");
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(userDirectory);
            _resolution = new ToolsetResolution(rootPath, cliPath, dataDirectory, userDirectory, null);
        }

        public Task<ToolsetResolution> ResolveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_resolution);
    }

    private sealed class StubSerialPortService : ISerialPortService
    {
        private readonly IReadOnlyList<ConnectedSerialPort> _ports;

        public StubSerialPortService(IReadOnlyList<ConnectedSerialPort> ports)
        {
            _ports = ports;
        }

        public IReadOnlyList<ConnectedSerialPort> ListPorts() => _ports;
    }

    private sealed class CapturingProcessRunner : IProcessRunner
    {
        private readonly ProcessExecutionResult _result;
        private readonly Action<ProcessExecutionRequest>? _onRun;

        public CapturingProcessRunner(ProcessExecutionResult result, Action<ProcessExecutionRequest>? onRun = null)
        {
            _result = result;
            _onRun = onRun;
        }

        public ProcessExecutionRequest? LastRequest { get; private set; }

        public Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            _onRun?.Invoke(request);
            return Task.FromResult(_result);
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
