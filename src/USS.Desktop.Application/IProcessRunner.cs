namespace USS.Desktop.Application;

public interface IProcessRunner
{
    Task<ProcessExecutionResult> RunAsync(
        ProcessExecutionRequest request,
        CancellationToken cancellationToken = default);
}
