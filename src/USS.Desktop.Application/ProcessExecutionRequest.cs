namespace USS.Desktop.Application;

public sealed record ProcessExecutionRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> EnvironmentVariables);
