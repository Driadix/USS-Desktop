using USS.Desktop.Domain;

namespace USS.Desktop.Application;

public interface IArduinoCliWorkflowService
{
    Task<WorkflowResult> CompileAsync(ProjectContext project, CancellationToken cancellationToken = default);

    Task<WorkflowResult> UploadAsync(
        ProjectContext project,
        string? portOverride,
        CancellationToken cancellationToken = default);

    Task<WorkflowResult> CompileAndUploadAsync(
        ProjectContext project,
        string? portOverride,
        CancellationToken cancellationToken = default);
}
