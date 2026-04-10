namespace USS.Desktop.Application;

public sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration);
