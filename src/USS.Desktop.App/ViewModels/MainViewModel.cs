using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using USS.Desktop.Application;
using USS.Desktop.App.Services;
using USS.Desktop.Domain;

namespace USS.Desktop.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProjectWorkspaceService _workspaceService;
    private readonly IArduinoCliWorkflowService _workflowService;
    private readonly IToolsetResolver _toolsetResolver;
    private readonly ISerialPortService _serialPortService;
    private readonly IFolderPicker _folderPicker;
    private readonly IRecentProjectsStore _recentProjectsStore;
    private ProjectContext? _currentProject;

    public MainViewModel(
        IProjectWorkspaceService workspaceService,
        IArduinoCliWorkflowService workflowService,
        IToolsetResolver toolsetResolver,
        ISerialPortService serialPortService,
        IFolderPicker folderPicker,
        IRecentProjectsStore recentProjectsStore)
    {
        _workspaceService = workspaceService;
        _workflowService = workflowService;
        _toolsetResolver = toolsetResolver;
        _serialPortService = serialPortService;
        _folderPicker = folderPicker;
        _recentProjectsStore = recentProjectsStore;

        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, CanEditWorkspace);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanEditWorkspace);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        CompileCommand = new AsyncRelayCommand(CompileAsync, CanRunWorkflow);
        UploadCommand = new AsyncRelayCommand(UploadAsync, CanRunWorkflow);
        CompileAndUploadCommand = new AsyncRelayCommand(CompileAndUploadAsync, CanRunWorkflow);
        OpenRecentProjectCommand = new AsyncRelayCommand<string?>(OpenRecentProjectAsync, path => CanEditWorkspace() && !string.IsNullOrWhiteSpace(path));
    }

    public ObservableCollection<string> Issues { get; } = new();

    public ObservableCollection<string> RecentProjects { get; } = new();

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    public IAsyncRelayCommand CompileCommand { get; }

    public IAsyncRelayCommand UploadCommand { get; }

    public IAsyncRelayCommand CompileAndUploadCommand { get; }

    public IAsyncRelayCommand<string?> OpenRecentProjectCommand { get; }

    [ObservableProperty]
    private string _selectedProjectPath = "No project selected";

    [ObservableProperty]
    private string _projectStatus = "Open a folder to inspect Arduino project status.";

    [ObservableProperty]
    private string _projectName = "No project";

    [ObservableProperty]
    private string _projectKind = "Unknown";

    [ObservableProperty]
    private string _projectFamily = "Unknown";

    [ObservableProperty]
    private string _activeProfile = "-";

    [ObservableProperty]
    private string _fqbn = "-";

    [ObservableProperty]
    private string _discoveryLabel = "Idle";

    [ObservableProperty]
    private string _diagnosticsSummary = "Diagnostics not loaded yet.";

    [ObservableProperty]
    private string _sessionLog = "Session log will appear here.";

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private string _importProjectName = string.Empty;

    [ObservableProperty]
    private string _bootstrapProfileName = "main";

    [ObservableProperty]
    private string _bootstrapFqbn = string.Empty;

    [ObservableProperty]
    private string _bootstrapPlatformIdentifier = string.Empty;

    [ObservableProperty]
    private string _bootstrapPlatformVersion = string.Empty;

    [ObservableProperty]
    private string _bootstrapPlatformIndexUrl = string.Empty;

    [ObservableProperty]
    private string _bootstrapLibraries = string.Empty;

    [ObservableProperty]
    private string _bootstrapFamily = "Unknown";

    [ObservableProperty]
    private bool _showImportPanel;

    [ObservableProperty]
    private bool _showBootstrapFields;

    [ObservableProperty]
    private string _importActionLabel = "Create uss.yaml";

    [ObservableProperty]
    private string _importHint = "Import actions will appear after opening a project.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showRecentProjectsEmptyState = true;

    public async Task InitializeAsync()
    {
        await RefreshDiagnosticsAsync();
        await LoadRecentProjectsAsync();
        AppendLog("Application ready.");
    }

    partial void OnIsBusyChanged(bool value)
    {
        OpenFolderCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        CompileCommand.NotifyCanExecuteChanged();
        UploadCommand.NotifyCanExecuteChanged();
        CompileAndUploadCommand.NotifyCanExecuteChanged();
        OpenRecentProjectCommand.NotifyCanExecuteChanged();
    }

    partial void OnBootstrapFqbnChanged(string value)
    {
        var family = ProjectFamilyDetector.FromFqbn(value);
        BootstrapFamily = family.ToDisplayName();

        if (string.IsNullOrWhiteSpace(BootstrapPlatformIdentifier))
        {
            var segments = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                BootstrapPlatformIdentifier = $"{segments[0]}:{segments[1]}";
            }
        }
    }

    private bool CanEditWorkspace() => !IsBusy;

    private bool CanImport() =>
        !IsBusy && _currentProject is not null && (_currentProject.CanCreateUssConfiguration || _currentProject.CanBootstrapSketchProject);

    private bool CanRunWorkflow() => !IsBusy && _currentProject?.CanCompile == true;

    private async Task OpenFolderAsync()
    {
        var folder = _folderPicker.PickFolder(_currentProject?.Files.ProjectDirectory);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        await LoadProjectAsync(folder);
    }

    private async Task OpenRecentProjectAsync(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        await LoadProjectAsync(folder);
    }

    private async Task RefreshAsync()
    {
        if (_currentProject is not null)
        {
            await LoadProjectAsync(_currentProject.Files.ProjectDirectory, updateRecentProjects: false);
        }
        else
        {
            await RefreshDiagnosticsAsync();
            await LoadRecentProjectsAsync();
        }
    }

    private async Task ImportAsync()
    {
        if (_currentProject is null)
        {
            return;
        }

        try
        {
            IsBusy = true;

            ProjectContext updatedProject;
            if (_currentProject.CanBootstrapSketchProject)
            {
                updatedProject = await _workspaceService.BootstrapArduinoProjectAsync(
                    new BootstrapArduinoProjectRequest(
                        _currentProject.Files.ProjectDirectory,
                        ImportProjectName,
                        BootstrapProfileName,
                        BootstrapFqbn,
                        BootstrapPlatformIdentifier,
                        BootstrapPlatformVersion,
                        BootstrapPlatformIndexUrl,
                        ParseLibraries(BootstrapLibraries)));

                AppendLog($"Bootstrapped sketch.yaml and uss.yaml in {_currentProject.Files.ProjectDirectory}.");
            }
            else
            {
                updatedProject = await _workspaceService.CreateUssConfigurationAsync(
                    new CreateUssConfigurationRequest(_currentProject.Files.ProjectDirectory, ImportProjectName));

                AppendLog($"Created uss.yaml in {_currentProject.Files.ProjectDirectory}.");
            }

            ApplyProject(updatedProject);
            await AddRecentProjectAsync(updatedProject.Files.ProjectDirectory);
            await RefreshDiagnosticsAsync();
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog($"Import failed: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CompileAsync() => RunWorkflowAsync("Compile", (project, progress) => _workflowService.CompileAsync(project, progress));

    private Task UploadAsync() => RunWorkflowAsync("Upload", (project, progress) => _workflowService.UploadAsync(project, SelectedPort, progress));

    private Task CompileAndUploadAsync() =>
        RunWorkflowAsync("Compile + Flash", (project, progress) => _workflowService.CompileAndUploadAsync(project, SelectedPort, progress));

    private async Task RunWorkflowAsync(
        string actionLabel,
        Func<ProjectContext, IProgress<string>, Task<WorkflowResult>> workflow)
    {
        if (_currentProject is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SessionLog = string.Empty;
            AppendLog($"{actionLabel} started.");
            var progress = new Progress<string>(AppendLog);

            var result = await workflow(_currentProject, progress);
            ProjectStatus = result.Summary;
            AppendLog($"{actionLabel} finished: {result.Summary}");

            if (!string.IsNullOrWhiteSpace(result.LogFilePath))
            {
                AppendLog($"Project log: {result.LogFilePath}");
            }

            _currentProject = await _workspaceService.OpenAsync(_currentProject.Files.ProjectDirectory);
            ApplyProject(_currentProject, preserveLog: true);
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog($"{actionLabel} failed: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
            await RefreshDiagnosticsAsync();
        }
    }

    private async Task LoadProjectAsync(string folderPath, bool updateRecentProjects = true)
    {
        try
        {
            IsBusy = true;
            var project = await _workspaceService.OpenAsync(folderPath);
            ApplyProject(project);
            await RefreshDiagnosticsAsync();

            if (updateRecentProjects)
            {
                await AddRecentProjectAsync(project.Files.ProjectDirectory);
            }

            AppendLog($"Opened {project.Files.ProjectDirectory}.");
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog($"Open failed: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyProject(ProjectContext project, bool preserveLog = false)
    {
        _currentProject = project;
        SelectedProjectPath = project.Files.ProjectDirectory;
        ProjectName = project.DisplayName;
        ProjectKind = project.UssConfiguration?.Project.Kind ?? (project.SketchConfiguration is not null ? "arduino" : "unknown");
        ProjectFamily = project.Family.ToDisplayName();
        ActiveProfile = project.ActiveProfileName ?? "-";
        Fqbn = project.ActiveProfile?.Fqbn ?? "-";
        DiscoveryLabel = project.DiscoveryKind switch
        {
            ProjectDiscoveryKind.ManagedProject => "Managed USS Project",
            ProjectDiscoveryKind.SketchProjectNeedsImport => "Sketch Requires USS Import",
            ProjectDiscoveryKind.SketchFolderNeedsBootstrap => "Sketch Folder Requires Bootstrap",
            _ => "Unsupported Folder"
        };

        ProjectStatus = project.DiscoveryKind switch
        {
            ProjectDiscoveryKind.ManagedProject when project.HasIssues => "Project opened with validation issues.",
            ProjectDiscoveryKind.ManagedProject => "Project is ready for compile and flash.",
            ProjectDiscoveryKind.SketchProjectNeedsImport => "sketch.yaml found. Create uss.yaml to manage this folder in USS.",
            ProjectDiscoveryKind.SketchFolderNeedsBootstrap => "Arduino sketch detected. Bootstrap sketch.yaml and uss.yaml to make it deterministic.",
            _ => "This folder is not supported by USS v1."
        };

        Issues.Clear();
        if (project.Issues.Count == 0)
        {
            Issues.Add("No validation issues.");
        }
        else
        {
            foreach (var issue in project.Issues)
            {
                Issues.Add(issue.Message);
            }
        }

        ShowImportPanel = project.CanCreateUssConfiguration || project.CanBootstrapSketchProject;
        ShowBootstrapFields = project.CanBootstrapSketchProject;
        ImportActionLabel = project.CanBootstrapSketchProject ? "Create sketch.yaml + uss.yaml" : "Create uss.yaml";
        ImportHint = project.CanBootstrapSketchProject
            ? "Provide the pinned Arduino profile details for this sketch folder."
            : "USS can generate uss.yaml from the existing sketch.yaml profile.";

        ImportProjectName = project.DisplayName;
        BootstrapProfileName = "main";
        BootstrapFqbn = string.Empty;
        BootstrapPlatformIdentifier = string.Empty;
        BootstrapPlatformVersion = string.Empty;
        BootstrapPlatformIndexUrl = string.Empty;
        BootstrapLibraries = string.Empty;
        BootstrapFamily = "Unknown";

        if (!preserveLog)
        {
            SessionLog = $"Opened {project.DisplayName}.{Environment.NewLine}{ProjectStatus}";
        }
    }

    private async Task RefreshDiagnosticsAsync()
    {
        var toolset = await _toolsetResolver.ResolveAsync();
        var ports = _serialPortService.ListPorts();

        AvailablePorts.Clear();
        foreach (var port in ports)
        {
            AvailablePorts.Add(port.Address);
        }

        DiagnosticsSummary = string.Join(
            Environment.NewLine,
            new[]
            {
                $"Arduino CLI: {toolset.ArduinoCliPath ?? "Not found"}",
                $"CLI data dir: {toolset.ArduinoDataDirectory}",
                $"CLI user dir: {toolset.ArduinoUserDirectory}",
                $"Ports: {(ports.Count == 0 ? "none detected" : string.Join(", ", ports.Select(port => port.Address)))}"
            });
    }

    private async Task LoadRecentProjectsAsync()
    {
        var recentProjects = await _recentProjectsStore.LoadAsync();
        RecentProjects.Clear();
        foreach (var project in recentProjects.Where(Directory.Exists))
        {
            RecentProjects.Add(project);
        }

        ShowRecentProjectsEmptyState = RecentProjects.Count == 0;
    }

    private async Task AddRecentProjectAsync(string projectPath)
    {
        var projects = RecentProjects
            .Where(path => !string.Equals(path, projectPath, StringComparison.OrdinalIgnoreCase))
            .Prepend(projectPath)
            .Take(8)
            .ToArray();

        RecentProjects.Clear();
        foreach (var project in projects)
        {
            RecentProjects.Add(project);
        }

        ShowRecentProjectsEmptyState = RecentProjects.Count == 0;
        await _recentProjectsStore.SaveAsync(projects);
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        SessionLog = SessionLog == "Session log will appear here."
            ? line
            : $"{SessionLog}{Environment.NewLine}{line}";
    }

    private static IReadOnlyList<SketchLibraryReference> ParseLibraries(string rawValue)
    {
        return rawValue
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLibrary)
            .ToArray();
    }

    private static SketchLibraryReference ParseLibrary(string rawValue)
    {
        var trimmed = rawValue.Trim();
        var openParenIndex = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
        if (openParenIndex > 0 && trimmed.EndsWith(')'))
        {
            return new SketchLibraryReference(
                trimmed[..openParenIndex].Trim(),
                trimmed[(openParenIndex + 2)..^1].Trim());
        }

        return new SketchLibraryReference(trimmed, string.Empty);
    }
}
