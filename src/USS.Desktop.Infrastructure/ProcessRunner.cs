using System.Diagnostics;
using System.Text;
using USS.Desktop.Application;

namespace USS.Desktop.Infrastructure;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environmentVariable in request.EnvironmentVariables)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value ?? string.Empty;
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                stdoutClosed.TrySetResult();
                return;
            }

            stdout.AppendLine(eventArgs.Data);
            request.OutputProgress?.Report(new ProcessOutputLine(ProcessOutputKind.StandardOutput, eventArgs.Data));
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                stderrClosed.TrySetResult();
                return;
            }

            stderr.AppendLine(eventArgs.Data);
            request.OutputProgress?.Report(new ProcessOutputLine(ProcessOutputKind.StandardError, eventArgs.Data));
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process '{request.FileName}'.");
        }

        var stopwatch = Stopwatch.StartNew();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task);
        stopwatch.Stop();

        return new ProcessExecutionResult(
            process.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            stopwatch.Elapsed);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
