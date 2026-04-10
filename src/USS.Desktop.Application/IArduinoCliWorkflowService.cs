using USS.Desktop.Domain;

namespace USS.Desktop.Application;

public interface IArduinoCliWorkflowService
{
    Task<WorkflowResult> CompileAsync(
        ProjectContext project,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowResult> UploadAsync(
        ProjectContext project,
        string? portOverride,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);

    Task<WorkflowResult> CompileAndUploadAsync(
        ProjectContext project,
        string? portOverride,
        IProgress<string>? outputProgress = null,
        CancellationToken cancellationToken = default);
}
