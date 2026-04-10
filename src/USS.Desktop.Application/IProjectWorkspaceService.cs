using USS.Desktop.Domain;

namespace USS.Desktop.Application;

public interface IProjectWorkspaceService
{
    Task<ProjectContext> OpenAsync(string folderPath, CancellationToken cancellationToken = default);

    Task<ProjectContext> CreateUssConfigurationAsync(
        CreateUssConfigurationRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectContext> BootstrapArduinoProjectAsync(
        BootstrapArduinoProjectRequest request,
        CancellationToken cancellationToken = default);
}
