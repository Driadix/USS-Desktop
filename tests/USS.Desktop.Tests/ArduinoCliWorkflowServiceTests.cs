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
        Assert.DoesNotContain("--build-path", processRunner.LastRequest.Arguments);
        Assert.Contains("--output-dir", processRunner.LastRequest.Arguments);
        Assert.Equal(Path.Combine(tempDirectory.Path, "build", "out"), GetArgumentValue(processRunner.LastRequest.Arguments, "--output-dir"));
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "data"), processRunner.LastRequest.WorkingDirectory);
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "data"), processRunner.LastRequest.EnvironmentVariables["ARDUINO_DIRECTORIES_DATA"]);
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "data", "staging"), processRunner.LastRequest.EnvironmentVariables["ARDUINO_DIRECTORIES_DOWNLOADS"]);
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "user"), processRunner.LastRequest.EnvironmentVariables["ARDUINO_DIRECTORIES_USER"]);
        Assert.Equal(Path.Combine(tempDirectory.Path, "arduino-data", "data", "build-cache"), processRunner.LastRequest.EnvironmentVariables["ARDUINO_BUILD_CACHE_PATH"]);
        Assert.True(Directory.Exists(Path.Combine(tempDirectory.Path, "arduino-data", "data", "build-cache")));
        Assert.True(Directory.Exists(Path.Combine(tempDirectory.Path, "arduino-data", "data", "staging")));
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

        var progress = new CollectingStringProgress();
        var project = CreateManagedProject(tempDirectory.Path);

        var result = await workflowService.CompileAsync(project, progress);

        Assert.True(result.Success);
        Assert.Contains(progress.Lines, line => line.StartsWith("CLI CMD | ", StringComparison.Ordinal));
        Assert.Contains("CLI OUT | Detecting libraries", progress.Lines);
        Assert.Contains("CLI ERR | warning: deprecated", progress.Lines);
        Assert.Contains("CLI EXIT | code 0", progress.Lines);
    }

    [Fact]
    public async Task CompileAsync_ReportsProcessOutputWithoutPostingToCurrentSynchronizationContext()
    {
        using var tempDirectory = new TestDirectory();
        var synchronizationContext = new CountingSynchronizationContext();
        var postedProcessOutput = false;
        var processRunner = new CapturingProcessRunner(
            new ProcessExecutionResult(0, "compile ok", string.Empty, TimeSpan.FromSeconds(1)),
            request =>
            {
                var postsBeforeReport = synchronizationContext.PostCount;
                request.OutputProgress?.Report(new ProcessOutputLine(ProcessOutputKind.StandardOutput, "Direct output"));
                postedProcessOutput = synchronizationContext.PostCount != postsBeforeReport;
            });

        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);

        try
        {
            var progress = new CollectingStringProgress();
            var project = CreateManagedProject(tempDirectory.Path);
            var result = await workflowService.CompileAsync(project, progress);

            Assert.True(result.Success);
            Assert.False(postedProcessOutput);
            Assert.Contains("CLI OUT | Direct output", progress.Lines);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public async Task CompileAsync_WithCleanAndVerbose_AddsRequestedFlags()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "compile ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.CompileAsync(project, clean: true, verbose: true);

        Assert.True(result.Success);
        Assert.Contains("--clean", processRunner.LastRequest!.Arguments);
        Assert.Contains("--verbose", processRunner.LastRequest.Arguments);
    }

    [Fact]
    public async Task UploadAsync_WithAutoSelection_UsesFirstDetectedPort()
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
        var result = await workflowService.UploadAsync(project, portOverride: "AUTO");

        Assert.True(result.Success);
        Assert.NotNull(processRunner.LastRequest);
        Assert.Contains("--port", processRunner.LastRequest!.Arguments);
        Assert.Contains("COM3", processRunner.LastRequest.Arguments);
    }

    [Fact]
    public async Task UploadAsync_WithoutOverride_UsesFirstDetectedPort()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "upload ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(new[]
            {
                new ConnectedSerialPort("COM8", "COM8"),
                new ConnectedSerialPort("COM9", "COM9")
            }));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.UploadAsync(project, portOverride: null);

        Assert.True(result.Success);
        Assert.NotNull(processRunner.LastRequest);
        Assert.Contains("--port", processRunner.LastRequest!.Arguments);
        Assert.Contains("COM8", processRunner.LastRequest.Arguments);
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

    [Fact]
    public async Task UploadAsync_WithVerbose_AddsVerboseFlag()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "upload ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.UploadAsync(project, "COM7", verbose: true);

        Assert.True(result.Success);
        Assert.Contains("--verbose", processRunner.LastRequest!.Arguments);
        Assert.DoesNotContain("--clean", processRunner.LastRequest.Arguments);
    }

    [Fact]
    public async Task CompileAndUploadAsync_WithCleanAndVerbose_UsesCleanOnlyForCompileStep()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new CapturingProcessRunner(new ProcessExecutionResult(0, "ok", string.Empty, TimeSpan.FromSeconds(1)));
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var project = CreateManagedProject(tempDirectory.Path);
        var result = await workflowService.CompileAndUploadAsync(project, "COM7", clean: true, verbose: true);

        Assert.True(result.Success);
        Assert.Equal(2, processRunner.Requests.Count);
        Assert.Contains("--clean", processRunner.Requests[0].Arguments);
        Assert.Contains("--verbose", processRunner.Requests[0].Arguments);
        Assert.DoesNotContain("--clean", processRunner.Requests[1].Arguments);
        Assert.Contains("--verbose", processRunner.Requests[1].Arguments);
    }

    [Fact]
    public async Task CompileAsync_WhenCancelled_RemovesLockFile()
    {
        using var tempDirectory = new TestDirectory();
        var processRunner = new BlockingProcessRunner();
        var workflowService = new ArduinoCliWorkflowService(
            new StubToolsetResolver(tempDirectory.Path),
            processRunner,
            new StubSerialPortService(Array.Empty<ConnectedSerialPort>()));

        var project = CreateManagedProject(tempDirectory.Path);
        using var cancellationTokenSource = new CancellationTokenSource();
        var compileTask = workflowService.CompileAsync(project, cancellationToken: cancellationTokenSource.Token);

        await processRunner.Started.Task;
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => compileTask);
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, "build", "work", "uss.lock")));
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

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == name)
            {
                return arguments[index + 1];
            }
        }

        return null;
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

        public List<ProcessExecutionRequest> Requests { get; } = new();

        public Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            Requests.Add(request);
            _onRun?.Invoke(request);
            return Task.FromResult(_result);
        }
    }

    private sealed class CollectingStringProgress : IProgress<string>
    {
        public List<string> Lines { get; } = new();

        public void Report(string value)
        {
            Lines.Add(value);
        }
    }

    private sealed class CountingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            d(state);
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

    private sealed class BlockingProcessRunner : IProcessRunner
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ProcessExecutionResult> RunAsync(ProcessExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ProcessExecutionResult(0, string.Empty, string.Empty, TimeSpan.Zero);
        }
    }
}
