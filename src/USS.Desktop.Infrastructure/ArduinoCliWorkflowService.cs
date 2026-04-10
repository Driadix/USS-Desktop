using System.IO;
using System.Text;
using USS.Desktop.Application;
using USS.Desktop.Domain;

namespace USS.Desktop.Infrastructure;

public sealed class ArduinoCliWorkflowService : IArduinoCliWorkflowService
{
    private readonly IToolsetResolver _toolsetResolver;
    private readonly IProcessRunner _processRunner;
    private readonly ISerialPortService _serialPortService;

    public ArduinoCliWorkflowService(
        IToolsetResolver toolsetResolver,
        IProcessRunner processRunner,
        ISerialPortService serialPortService)
    {
        _toolsetResolver = toolsetResolver;
        _processRunner = processRunner;
        _serialPortService = serialPortService;
    }

    public Task<WorkflowResult> CompileAsync(
        ProjectContext project,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("compile", project, null, outputProgress, cancellationToken);

    public Task<WorkflowResult> UploadAsync(
        ProjectContext project,
        string? portOverride,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync("upload", project, portOverride, outputProgress, cancellationToken);

    public async Task<WorkflowResult> CompileAndUploadAsync(
        ProjectContext project,
        string? portOverride,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default)
    {
        var compileResult = await CompileAsync(project, outputProgress, cancellationToken);
        if (!compileResult.Success)
        {
            return compileResult with { OperationName = "compile + upload" };
        }

        var uploadResult = await UploadAsync(project, portOverride, outputProgress, cancellationToken);
        return uploadResult with
        {
            OperationName = "compile + upload",
            Summary = uploadResult.Success
                ? "Compile and upload completed successfully."
                : $"Compile succeeded, upload failed: {uploadResult.Summary}"
        };
    }

    private async Task<WorkflowResult> ExecuteAsync(
        string operationName,
        ProjectContext project,
        string? portOverride,
        IProgress<string>? outputProgress,
        CancellationToken cancellationToken)
    {
        var readinessFailure = Validate(project, operationName);
        if (readinessFailure is not null)
        {
            ReportFailure(readinessFailure, outputProgress);
            return readinessFailure;
        }

        var toolset = await _toolsetResolver.ResolveAsync(cancellationToken);
        if (!toolset.IsAvailable || toolset.ArduinoCliPath is null)
        {
            var failure = WorkflowResult.Failure(
                operationName,
                "arduino-cli is not available.",
                toolset.FailureMessage ?? "arduino-cli.exe could not be found. Add it under toolsets/ or install it into PATH.");
            ReportFailure(failure, outputProgress);
            return failure;
        }

        var artifacts = project.UssConfiguration!.Artifacts;
        var outputDirectory = EnsureDirectory(project.Files.ProjectDirectory, artifacts.OutputDirectory);
        var logDirectory = EnsureDirectory(project.Files.ProjectDirectory, artifacts.LogDirectory);
        var workRootDirectory = EnsureDirectory(project.Files.ProjectDirectory, artifacts.WorkDirectory);
        var buildPath = EnsureDirectory(workRootDirectory, project.ActiveProfileName!);

        var lockFilePath = Path.Combine(workRootDirectory, "uss.lock");
        if (File.Exists(lockFilePath))
        {
            return WorkflowResult.Failure(
                operationName,
                "The project is already busy.",
                "build/work/uss.lock already exists. Remove the stale lock file if no other USS operation is running.");
        }

        var timestamp = DateTimeOffset.Now;
        var logFilePath = Path.Combine(logDirectory, $"{timestamp:yyyyMMdd-HHmmss}-{operationName.Replace(' ', '-')}.log");
        var arguments = BuildArguments(operationName, project, buildPath, outputDirectory, portOverride);
        if (arguments is null)
        {
            var failure = WorkflowResult.Failure(
                operationName,
                "A serial port is required for upload.",
                "No upload port could be resolved. Connect a board or choose a specific COM port.");
            ReportFailure(failure, outputProgress);
            return failure;
        }

        var request = new ProcessExecutionRequest(
            toolset.ArduinoCliPath,
            arguments,
            project.Files.ProjectDirectory,
            new Dictionary<string, string?>
            {
                ["ARDUINO_DIRECTORIES_DATA"] = toolset.ArduinoDataDirectory,
                ["ARDUINO_DIRECTORIES_USER"] = toolset.ArduinoUserDirectory
            },
            CreateProcessOutputProgress(outputProgress));

        var commandLine = $"{toolset.ArduinoCliPath} {string.Join(" ", arguments.Select(QuoteArgument))}";
        outputProgress?.Report($"CLI CMD | {commandLine}");

        await File.WriteAllTextAsync(
            lockFilePath,
            $"{timestamp:O}{Environment.NewLine}{operationName}{Environment.NewLine}{commandLine}",
            cancellationToken);

        try
        {
            var result = await _processRunner.RunAsync(request, cancellationToken);
            var transcript = BuildTranscript(operationName, project, request, commandLine, result);
            await File.WriteAllTextAsync(logFilePath, transcript, cancellationToken);
            outputProgress?.Report($"CLI EXIT | code {result.ExitCode}");

            var summary = result.ExitCode == 0
                ? $"{operationName} completed successfully."
                : $"{operationName} failed with exit code {result.ExitCode}.";

            return new WorkflowResult(
                operationName,
                result.ExitCode == 0,
                summary,
                transcript,
                commandLine,
                logFilePath,
                result.ExitCode);
        }
        finally
        {
            if (File.Exists(lockFilePath))
            {
                File.Delete(lockFilePath);
            }
        }
    }

    private WorkflowResult? Validate(ProjectContext project, string operationName)
    {
        if (project.UssConfiguration is null)
        {
            return WorkflowResult.Failure(operationName, "uss.yaml is required.", "Import this project into USS before running workflows.");
        }

        if (project.ActiveProfile is null || string.IsNullOrWhiteSpace(project.ActiveProfileName))
        {
            return WorkflowResult.Failure(operationName, "No active sketch profile is available.", "The project configuration is incomplete.");
        }

        if (project.HasIssues)
        {
            var transcript = string.Join(Environment.NewLine, project.Issues.Select(issue => $"- {issue.Message}"));
            return WorkflowResult.Failure(operationName, "Project validation failed.", transcript);
        }

        return null;
    }

    private IReadOnlyList<string>? BuildArguments(
        string operationName,
        ProjectContext project,
        string buildPath,
        string outputDirectory,
        string? portOverride)
    {
        var arguments = new List<string>
        {
            operationName,
            project.Files.ProjectDirectory,
            "--profile",
            project.ActiveProfileName!,
            "--no-color"
        };

        if (string.Equals(operationName, "compile", StringComparison.OrdinalIgnoreCase))
        {
            arguments.AddRange(new[] { "--build-path", buildPath, "--output-dir", outputDirectory });
            return arguments;
        }

        var resolvedPort = ResolveUploadPort(project, portOverride);
        if (resolvedPort is null)
        {
            return null;
        }

        arguments.AddRange(new[] { "--input-dir", outputDirectory, "--port", resolvedPort });
        if (!string.IsNullOrWhiteSpace(project.ActiveProfile!.Protocol))
        {
            arguments.Add("--protocol");
            arguments.Add(project.ActiveProfile.Protocol);
        }

        if (project.UssConfiguration!.Upload.VerifyAfterUpload)
        {
            arguments.Add("--verify");
        }

        return arguments;
    }

    private string? ResolveUploadPort(ProjectContext project, string? portOverride)
    {
        if (!string.IsNullOrWhiteSpace(portOverride))
        {
            return portOverride.Trim();
        }

        if (!string.IsNullOrWhiteSpace(project.UssConfiguration?.Upload.Port)
            && !string.Equals(project.UssConfiguration.Upload.Port, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return project.UssConfiguration.Upload.Port;
        }

        if (!string.IsNullOrWhiteSpace(project.ActiveProfile?.Port))
        {
            return project.ActiveProfile.Port;
        }

        var ports = _serialPortService.ListPorts();
        return ports.Count == 1 ? ports[0].Address : null;
    }

    private static string EnsureDirectory(string rootPath, string relativePath)
    {
        var fullPath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.GetFullPath(Path.Combine(rootPath, relativePath));

        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private static string BuildTranscript(
        string operationName,
        ProjectContext project,
        ProcessExecutionRequest request,
        string commandLine,
        ProcessExecutionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Operation: {operationName}");
        builder.AppendLine($"Project: {project.DisplayName}");
        builder.AppendLine($"Directory: {project.Files.ProjectDirectory}");
        builder.AppendLine($"Profile: {project.ActiveProfileName}");
        builder.AppendLine($"Command: {commandLine}");
        builder.AppendLine($"Working directory: {request.WorkingDirectory}");
        builder.AppendLine($"Exit code: {result.ExitCode}");
        builder.AppendLine($"Duration: {result.Duration}");
        builder.AppendLine();
        builder.AppendLine("stdout");
        builder.AppendLine(result.StandardOutput.TrimEnd());

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            builder.AppendLine();
            builder.AppendLine("stderr");
            builder.AppendLine(result.StandardError.TrimEnd());
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string QuoteArgument(string argument) =>
        argument.Contains(' ', StringComparison.Ordinal)
            ? $"\"{argument}\""
            : argument;

    private static IProgress<ProcessOutputLine>? CreateProcessOutputProgress(IProgress<string>? outputProgress)
    {
        if (outputProgress is null)
        {
            return null;
        }

        return new Progress<ProcessOutputLine>(line =>
        {
            var prefix = line.Kind == ProcessOutputKind.StandardError ? "CLI ERR" : "CLI OUT";
            outputProgress.Report($"{prefix} | {line.Text}");
        });
    }

    private static void ReportFailure(WorkflowResult failure, IProgress<string>? outputProgress)
    {
        if (outputProgress is null)
        {
            return;
        }

        outputProgress.Report($"CLI FAIL | {failure.Summary}");
        foreach (var line in failure.Transcript.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            outputProgress.Report($"CLI INFO | {line}");
        }
    }
}
