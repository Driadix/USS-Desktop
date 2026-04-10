namespace USS.Desktop.Domain;

public sealed record WorkflowResult(
    string OperationName,
    bool Success,
    string Summary,
    string Transcript,
    string CommandLine,
    string? LogFilePath,
    int? ExitCode)
{
    public static WorkflowResult Failure(string operationName, string summary, string transcript) =>
        new(operationName, false, summary, transcript, string.Empty, null, null);
}
