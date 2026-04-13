using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using USS.Desktop.Application;
using USS.Desktop.App.Services;
using USS.Desktop.Domain;

namespace USS.Desktop.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const string CompileOperationKey = "compile";
    private const string UploadOperationKey = "upload";
    private const string CompileAndUploadOperationKey = "compile-upload";
    private const string AutoPortSelection = "AUTO";
    private const int MaxSessionLogLines = 2000;
    private static readonly TimeSpan WorkflowLogFlushInterval = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PortPollingInterval = TimeSpan.FromSeconds(2);

    private readonly IProjectWorkspaceService _workspaceService;
    private readonly IArduinoCliWorkflowService _workflowService;
    private readonly IToolsetResolver _toolsetResolver;
    private readonly ISerialPortService _serialPortService;
    private readonly IFolderPicker _folderPicker;
    private readonly IConfirmationService _confirmationService;
    private readonly IRecentProjectsStore _recentProjectsStore;
    private readonly LocalizationService _localization;
    private readonly UserPreferencesService _userPreferencesService;
    private readonly IThemeEditorDialogService _themeEditorDialogService;
    private CancellationTokenSource? _activeWorkflowCancellation;
    private Task<WorkflowResult>? _activeWorkflowTask;
    private ProjectContext? _currentProject;
    private ToolsetResolution? _lastToolsetResolution;
    private IReadOnlyList<ConnectedSerialPort> _lastPorts = Array.Empty<ConnectedSerialPort>();
    private DispatcherTimer? _portPollingTimer;
    private bool _isRefreshingPorts;
    private bool _isSynchronizingPreferenceSelections;
    private bool? _lastUploadPortAvailable;
    private readonly Queue<string> _sessionLogLines = new();

    public MainViewModel(
        IProjectWorkspaceService workspaceService,
        IArduinoCliWorkflowService workflowService,
        IToolsetResolver toolsetResolver,
        ISerialPortService serialPortService,
        IFolderPicker folderPicker,
        IConfirmationService confirmationService,
        IRecentProjectsStore recentProjectsStore,
        LocalizationService localization,
        UserPreferencesService userPreferencesService,
        IThemeEditorDialogService themeEditorDialogService)
    {
        _workspaceService = workspaceService;
        _workflowService = workflowService;
        _toolsetResolver = toolsetResolver;
        _serialPortService = serialPortService;
        _folderPicker = folderPicker;
        _confirmationService = confirmationService;
        _recentProjectsStore = recentProjectsStore;
        _localization = localization;
        _userPreferencesService = userPreferencesService;
        _themeEditorDialogService = themeEditorDialogService;

        Localization = localization;

        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync, CanEditWorkspace);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanEditWorkspace);
        ImportCommand = new AsyncRelayCommand(ImportAsync, CanImport);
        DeleteLockCommand = new AsyncRelayCommand(DeleteLockAsync, CanDeleteLock);
        CompileCommand = new AsyncRelayCommand(CompileAsync, CanCompileWorkflow);
        UploadCommand = new AsyncRelayCommand(UploadAsync, CanUploadWorkflow);
        CompileAndUploadCommand = new AsyncRelayCommand(CompileAndUploadAsync, CanUploadWorkflow);
        StopCommand = new AsyncRelayCommand(StopAsync, CanStopWorkflow);
        ClearLogCommand = new RelayCommand(ClearLog);
        OpenThemeEditorCommand = new AsyncRelayCommand(OpenThemeEditorAsync);
        OpenProjectLocationCommand = new RelayCommand(OpenProjectLocation, CanOpenProjectLocation);
        OpenRecentProjectCommand = new AsyncRelayCommand<string?>(OpenRecentProjectAsync, path => CanEditWorkspace() && !string.IsNullOrWhiteSpace(path));

        _userPreferencesService.SettingsChanged += OnUserPreferencesChanged;

        RebuildPreferenceOptions();
        ApplyLocalizedShellState();
    }

    public LocalizationService Localization { get; }

    public ObservableCollection<string> Issues { get; } = new();

    public ObservableCollection<string> RecentProjects { get; } = new();

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public ObservableCollection<SelectionOption<AppLanguage>> LanguageOptions { get; } = new();

    public ObservableCollection<SelectionOption<AppThemeMode>> ThemeModeOptions { get; } = new();

    public IAsyncRelayCommand OpenFolderCommand { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public IAsyncRelayCommand ImportCommand { get; }

    public IAsyncRelayCommand DeleteLockCommand { get; }

    public IAsyncRelayCommand CompileCommand { get; }

    public IAsyncRelayCommand UploadCommand { get; }

    public IAsyncRelayCommand CompileAndUploadCommand { get; }

    public IAsyncRelayCommand StopCommand { get; }

    public IRelayCommand ClearLogCommand { get; }

    public IAsyncRelayCommand OpenThemeEditorCommand { get; }

    public IRelayCommand OpenProjectLocationCommand { get; }

    public IAsyncRelayCommand<string?> OpenRecentProjectCommand { get; }

    [ObservableProperty]
    private string _selectedProjectPath = string.Empty;

    [ObservableProperty]
    private string _projectStatus = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _projectKind = string.Empty;

    [ObservableProperty]
    private string _projectFamily = string.Empty;

    [ObservableProperty]
    private string _activeProfile = "-";

    [ObservableProperty]
    private string _fqbn = "-";

    [ObservableProperty]
    private string _discoveryLabel = string.Empty;

    [ObservableProperty]
    private string _diagnosticsSummary = string.Empty;

    [ObservableProperty]
    private string _sessionLog = string.Empty;

    [ObservableProperty]
    private string _selectedPort = AutoPortSelection;

    [ObservableProperty]
    private bool _useCleanBuild;

    [ObservableProperty]
    private bool _useVerboseOutput;

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
    private string _bootstrapFamily = string.Empty;

    [ObservableProperty]
    private bool _showImportPanel;

    [ObservableProperty]
    private bool _showBootstrapFields;

    [ObservableProperty]
    private string _importActionLabel = string.Empty;

    [ObservableProperty]
    private string _importHint = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showRecentProjectsEmptyState = true;

    [ObservableProperty]
    private bool _hasLockFile;

    [ObservableProperty]
    private string _lockFilePath = string.Empty;

    [ObservableProperty]
    private bool _isStoppingOperation;

    [ObservableProperty]
    private SelectionOption<AppLanguage>? _selectedLanguage;

    [ObservableProperty]
    private SelectionOption<AppThemeMode>? _selectedThemeMode;

    [ObservableProperty]
    private string _activeOperationLabel = string.Empty;

    public async Task InitializeAsync()
    {
        ApplyLocalizedShellState();
        await RefreshDiagnosticsAsync();
        await LoadRecentProjectsAsync();
        EnsurePortPollingStarted();
        AppendLog(_localization["Log.AppReady"]);
        LogStateSnapshot("Initial UI state loaded.");
    }

    partial void OnIsBusyChanged(bool value)
    {
        OpenFolderCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ImportCommand.NotifyCanExecuteChanged();
        DeleteLockCommand.NotifyCanExecuteChanged();
        CompileCommand.NotifyCanExecuteChanged();
        UploadCommand.NotifyCanExecuteChanged();
        CompileAndUploadCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        OpenRecentProjectCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsStoppingOperationChanged(bool value)
    {
        StopCommand.NotifyCanExecuteChanged();
    }

    partial void OnBootstrapFqbnChanged(string value)
    {
        var family = ProjectFamilyDetector.FromFqbn(value);
        BootstrapFamily = LocalizeProjectFamily(family);

        if (string.IsNullOrWhiteSpace(BootstrapPlatformIdentifier))
        {
            var segments = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                BootstrapPlatformIdentifier = $"{segments[0]}:{segments[1]}";
            }
        }
    }

    partial void OnSelectedLanguageChanged(SelectionOption<AppLanguage>? value)
    {
        if (_isSynchronizingPreferenceSelections || value is null || value.Value == _userPreferencesService.CurrentLanguage)
        {
            return;
        }

        _ = ChangeLanguageAsync(value.Value);
    }

    partial void OnSelectedThemeModeChanged(SelectionOption<AppThemeMode>? value)
    {
        if (_isSynchronizingPreferenceSelections || value is null || value.Value == _userPreferencesService.CurrentThemeMode)
        {
            return;
        }

        _ = ChangeThemeModeAsync(value.Value);
    }

    partial void OnSelectedProjectPathChanged(string value)
    {
        OpenProjectLocationCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPortChanged(string value)
    {
        UploadCommand.NotifyCanExecuteChanged();
        CompileAndUploadCommand.NotifyCanExecuteChanged();
    }

    private bool CanEditWorkspace() => !IsBusy;

    private bool CanImport() =>
        !IsBusy && _currentProject is not null && (_currentProject.CanCreateUssConfiguration || _currentProject.CanBootstrapSketchProject);

    private bool CanDeleteLock() => !IsBusy && _currentProject is not null && HasLockFile;

    private bool CanCompileWorkflow() => !IsBusy && _currentProject?.CanCompile == true;

    private bool CanUploadWorkflow() => CanCompileWorkflow() && IsUploadPortAvailable();

    private bool CanStopWorkflow() => IsBusy && _activeWorkflowCancellation is not null && !IsStoppingOperation;

    private bool CanOpenProjectLocation() => Directory.Exists(SelectedProjectPath);

    private async Task OpenFolderAsync()
    {
        var folder = _folderPicker.PickFolder(_currentProject?.Files.ProjectDirectory);
        if (string.IsNullOrWhiteSpace(folder))
        {
            Log.Information("Open project folder dialog cancelled.");
            return;
        }

        Log.Information("Project folder selected. Folder={Folder}", folder);
        await LoadProjectAsync(folder);
    }

    private async Task OpenRecentProjectAsync(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        Log.Information("Opening recent project. Folder={Folder}", folder);
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
            ApplyLocalizedShellState();
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

                AppendLog(_localization.Format("Log.ImportBootstrapped", _currentProject.Files.ProjectDirectory));
            }
            else
            {
                updatedProject = await _workspaceService.CreateUssConfigurationAsync(
                    new CreateUssConfigurationRequest(_currentProject.Files.ProjectDirectory, ImportProjectName));

                AppendLog(_localization.Format("Log.ImportCreated", _currentProject.Files.ProjectDirectory));
            }

            ApplyProject(updatedProject);
            await AddRecentProjectAsync(updatedProject.Files.ProjectDirectory);
            await RefreshDiagnosticsAsync();
            LogStateSnapshot("Import completed.");
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.ImportFailed", exception.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task CompileAsync() =>
        RunWorkflowAsync(CompileOperationKey, (project, progress, cancellationToken) => _workflowService.CompileAsync(project, progress, clean: UseCleanBuild, verbose: UseVerboseOutput, cancellationToken: cancellationToken));

    private Task UploadAsync() =>
        RunWorkflowAsync(UploadOperationKey, (project, progress, cancellationToken) => _workflowService.UploadAsync(project, GetPortOverrideForWorkflow(), progress, verbose: UseVerboseOutput, cancellationToken: cancellationToken));

    private Task CompileAndUploadAsync() =>
        RunWorkflowAsync(CompileAndUploadOperationKey, (project, progress, cancellationToken) => _workflowService.CompileAndUploadAsync(project, GetPortOverrideForWorkflow(), progress, clean: UseCleanBuild, verbose: UseVerboseOutput, cancellationToken: cancellationToken));

    private async Task DeleteLockAsync()
    {
        if (_currentProject is null || !HasLockFile)
        {
            return;
        }

        var confirmed = _confirmationService.Confirm(
            _localization["Confirm.DeleteLock.Title"],
            _localization["Confirm.DeleteLock.Message"]);

        if (!confirmed)
        {
            return;
        }

        try
        {
            IsBusy = true;
            var deleted = await _workspaceService.DeleteLockFileAsync(_currentProject.Files.ProjectDirectory);
            AppendLog(deleted
                ? _localization["Log.DeleteLockDeleted"]
                : _localization["Log.DeleteLockMissing"]);

            await LoadProjectAsync(_currentProject.Files.ProjectDirectory, updateRecentProjects: false);
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.DeleteLockFailed", exception.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopAsync()
    {
        if (_activeWorkflowCancellation is null)
        {
            return;
        }

        try
        {
            IsStoppingOperation = true;
            ProjectStatus = _localization.Format("Workflow.Status.Stopping", ActiveOperationLabel);
            await CancelActiveOperationAsync(_localization["Log.StopRequested"]);
        }
        finally
        {
            IsStoppingOperation = false;
        }
    }

    private async Task OpenThemeEditorAsync()
    {
        await _themeEditorDialogService.OpenAsync();
    }

    private async Task RunWorkflowAsync(
        string operationKey,
        Func<ProjectContext, IProgress<string>, CancellationToken, Task<WorkflowResult>> workflow)
    {
        if (_currentProject is null)
        {
            return;
        }

        var actionLabel = LocalizeWorkflowAction(operationKey);
        var cleanEnabled = UseCleanBuild && !string.Equals(operationKey, UploadOperationKey, StringComparison.Ordinal);
        var verboseEnabled = UseVerboseOutput;
        _activeWorkflowCancellation = new CancellationTokenSource();
        BatchedLogProgress? progress = null;
        try
        {
            IsBusy = true;
            ActiveOperationLabel = actionLabel;
            SetSessionLogText(string.Empty);
            ProjectStatus = _localization.Format("Workflow.Status.Running", actionLabel);
            Log.Information(
                "Workflow starting. Operation={Operation} Clean={Clean} Verbose={Verbose} SelectedPort={SelectedPort}",
                operationKey,
                cleanEnabled,
                verboseEnabled,
                SelectedPort);
            AppendLog(_localization.Format("Log.WorkflowStarted", actionLabel));
            progress = new BatchedLogProgress(this);
            _activeWorkflowTask = workflow(_currentProject, progress, _activeWorkflowCancellation.Token);
            var result = await _activeWorkflowTask;
            progress.Flush();
            ProjectStatus = LocalizeWorkflowSummary(operationKey, result);
            AppendLog(_localization.Format("Log.WorkflowFinished", actionLabel, ProjectStatus));

            if (!string.IsNullOrWhiteSpace(result.LogFilePath))
            {
                AppendLog(_localization.Format("Log.ProjectLogPath", result.LogFilePath));
            }

            _currentProject = await _workspaceService.OpenAsync(_currentProject.Files.ProjectDirectory);
            ApplyProject(_currentProject, preserveLog: true, resetDrafts: false);
            LogStateSnapshot($"Workflow finished: {operationKey}.");
        }
        catch (OperationCanceledException)
        {
            progress?.Flush();
            ProjectStatus = _localization.Format("Log.WorkflowCancelledStatus", actionLabel);
            AppendLog(_localization.Format("Log.WorkflowCancelled", actionLabel));
            await RefreshCurrentProjectStateAsync(autoDeleteLockIfPresent: true);
            Log.Warning("Workflow cancelled. Operation={Operation}", operationKey);
        }
        catch (Exception exception)
        {
            progress?.Flush();
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.WorkflowFailed", actionLabel, exception.Message));
            Log.Error(exception, "Workflow failed. Operation={Operation}", operationKey);
        }
        finally
        {
            progress?.Dispose();
            _activeWorkflowTask = null;
            _activeWorkflowCancellation?.Dispose();
            _activeWorkflowCancellation = null;
            IsBusy = false;
            ActiveOperationLabel = ResolveInactiveOperationLabel();
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

            AppendLog(_localization.Format("Log.ProjectOpened", project.Files.ProjectDirectory));
            LogStateSnapshot("Project loaded.");
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.ProjectOpenFailed", exception.Message));
            Log.Error(exception, "Project open failed. Folder={FolderPath}", folderPath);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyProject(ProjectContext project, bool preserveLog = false, bool resetDrafts = true)
    {
        _currentProject = project;
        SelectedProjectPath = project.Files.ProjectDirectory;
        ProjectName = project.DisplayName;
        ProjectKind = LocalizeProjectKind(project.UssConfiguration?.Project.Kind, project.SketchConfiguration is not null);
        ProjectFamily = LocalizeProjectFamily(project.Family);
        ActiveProfile = project.ActiveProfileName ?? "-";
        Fqbn = project.ActiveProfile?.Fqbn ?? "-";
        DiscoveryLabel = project.DiscoveryKind switch
        {
            ProjectDiscoveryKind.ManagedProject => _localization["Project.Discovery.Managed"],
            ProjectDiscoveryKind.SketchProjectNeedsImport => _localization["Project.Discovery.SketchNeedsImport"],
            ProjectDiscoveryKind.SketchFolderNeedsBootstrap => _localization["Project.Discovery.SketchNeedsBootstrap"],
            _ => _localization["Project.Discovery.Unsupported"]
        };

        ProjectStatus = project.DiscoveryKind switch
        {
            ProjectDiscoveryKind.ManagedProject when project.HasIssues => _localization["Project.Status.ManagedIssues"],
            ProjectDiscoveryKind.ManagedProject => _localization["Project.Status.ManagedReady"],
            ProjectDiscoveryKind.SketchProjectNeedsImport => _localization["Project.Status.SketchNeedsImport"],
            ProjectDiscoveryKind.SketchFolderNeedsBootstrap => _localization["Project.Status.SketchNeedsBootstrap"],
            _ => _localization["Project.Status.Unsupported"]
        };

        Issues.Clear();
        if (project.Issues.Count == 0)
        {
            Issues.Add(_localization["Validation.None"]);
        }
        else
        {
            foreach (var issue in project.Issues)
            {
                Issues.Add(LocalizeValidationIssue(issue));
            }
        }

        ShowImportPanel = project.CanCreateUssConfiguration || project.CanBootstrapSketchProject;
        ShowBootstrapFields = project.CanBootstrapSketchProject;
        ImportActionLabel = project.CanBootstrapSketchProject
            ? _localization["Import.Action.Bootstrap"]
            : _localization["Import.Action.CreateUss"];
        ImportHint = project.CanBootstrapSketchProject
            ? _localization["Import.Hint.Bootstrap"]
            : _localization["Import.Hint.Import"];

        if (resetDrafts)
        {
            ImportProjectName = project.DisplayName;
            BootstrapProfileName = "main";
            BootstrapFqbn = string.Empty;
            BootstrapPlatformIdentifier = string.Empty;
            BootstrapPlatformVersion = string.Empty;
            BootstrapPlatformIndexUrl = string.Empty;
            BootstrapLibraries = string.Empty;
        }

        BootstrapFamily = LocalizeProjectFamily(ProjectFamilyDetector.FromFqbn(BootstrapFqbn));
        LockFilePath = Path.Combine(project.Files.ProjectDirectory, "build", "work", "uss.lock");
        HasLockFile = File.Exists(LockFilePath);
        DeleteLockCommand.NotifyCanExecuteChanged();
        OpenProjectLocationCommand.NotifyCanExecuteChanged();
        if (!IsBusy)
        {
            ActiveOperationLabel = ResolveInactiveOperationLabel();
        }

        if (!preserveLog)
        {
            SetSessionLogText($"{_localization.Format("Log.ProjectOpenedName", project.DisplayName)}{Environment.NewLine}{ProjectStatus}");
        }
    }

    private async Task RefreshDiagnosticsAsync()
    {
        _lastToolsetResolution = await _toolsetResolver.ResolveAsync();
        RefreshPortState(logChanges: false);
        Log.Information(
            "Diagnostics refreshed. ArduinoCliPath={ArduinoCliPath} PortCount={PortCount} Ports={Ports}",
            _lastToolsetResolution.ArduinoCliPath ?? _localization["Diagnostics.NotFound"],
            _lastPorts.Count,
            _lastPorts.Select(port => port.Address).ToArray());
    }

    private string BuildDiagnosticsSummary()
    {
        if (_lastToolsetResolution is null)
        {
            return _localization["Diagnostics.NotLoaded"];
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                _localization.Format("Diagnostics.CliPath", _lastToolsetResolution.ArduinoCliPath ?? _localization["Diagnostics.NotFound"]),
                _localization.Format("Diagnostics.CliData", _lastToolsetResolution.ArduinoDataDirectory),
                _localization.Format("Diagnostics.CliUser", _lastToolsetResolution.ArduinoUserDirectory),
                _localization.Format(
                    "Diagnostics.Ports",
                    _lastPorts.Count == 0
                        ? _localization["Diagnostics.NoPorts"]
                        : string.Join(", ", _lastPorts.Select(port => port.Address)))
            });
    }

    private void EnsurePortPollingStarted()
    {
        if (_portPollingTimer is not null)
        {
            return;
        }

        _portPollingTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = PortPollingInterval
        };
        _portPollingTimer.Tick += OnPortPollingTick;
        _portPollingTimer.Start();
    }

    private void OnPortPollingTick(object? sender, EventArgs e)
    {
        if (_isRefreshingPorts)
        {
            return;
        }

        try
        {
            _isRefreshingPorts = true;
            RefreshPortState(logChanges: true);
        }
        finally
        {
            _isRefreshingPorts = false;
        }
    }

    private void RefreshPortState(bool logChanges)
    {
        var previousPortAddresses = _lastPorts.Select(port => port.Address).ToArray();
        _lastPorts = _serialPortService.ListPorts();

        SyncAvailablePorts();
        DiagnosticsSummary = BuildDiagnosticsSummary();
        NotifyUploadCommandsChanged();

        if (!logChanges)
        {
            _lastUploadPortAvailable = IsUploadPortAvailable();
            return;
        }

        var currentPortAddresses = _lastPorts.Select(port => port.Address).ToArray();
        if (!previousPortAddresses.SequenceEqual(currentPortAddresses, StringComparer.OrdinalIgnoreCase))
        {
            AppendLog(_localization.Format(
                "Log.PortsChanged",
                currentPortAddresses.Length == 0
                    ? _localization["Diagnostics.NoPorts"]
                    : string.Join(", ", currentPortAddresses)));
        }

        var uploadPortAvailable = IsUploadPortAvailable();
        var canSurfaceUploadAvailability = _currentProject?.CanCompile == true && !IsBusy;
        if (_lastUploadPortAvailable != uploadPortAvailable)
        {
            var normalizedPort = NormalizeSelectedPort(SelectedPort);
            if (uploadPortAvailable && canSurfaceUploadAvailability)
            {
                AppendLog(_localization.Format(
                    "Log.UploadPortAvailable",
                    IsAutoPort(normalizedPort)
                        ? AutoPortSelection
                        : normalizedPort));
            }
            else if (canSurfaceUploadAvailability)
            {
                AppendLog(_localization.Format(
                    "Log.UploadPortUnavailable",
                    IsAutoPort(normalizedPort)
                        ? AutoPortSelection
                        : string.IsNullOrWhiteSpace(normalizedPort)
                            ? _localization["Actions.UploadPortManual"]
                            : normalizedPort));
            }
        }

        _lastUploadPortAvailable = uploadPortAvailable;
    }

    private void SyncAvailablePorts()
    {
        var desiredPorts = new List<string> { AutoPortSelection };
        desiredPorts.AddRange(_lastPorts.Select(port => port.Address));

        var normalizedSelectedPort = NormalizeSelectedPort(SelectedPort);
        if (!string.IsNullOrWhiteSpace(normalizedSelectedPort)
            && !desiredPorts.Contains(normalizedSelectedPort, StringComparer.OrdinalIgnoreCase))
        {
            desiredPorts.Add(normalizedSelectedPort);
        }

        if (AvailablePorts.SequenceEqual(desiredPorts, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        AvailablePorts.Clear();
        foreach (var port in desiredPorts)
        {
            AvailablePorts.Add(port);
        }
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
        WriteRuntimeLog(message);
        AppendFormattedLogLines(new[] { FormatLogLine(DateTime.Now, message) });
    }

    private void AppendFormattedLogLines(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return;
        }

        if (_sessionLogLines.Count == 1 && _sessionLogLines.Peek() == _localization["Log.Empty"])
        {
            _sessionLogLines.Clear();
        }

        foreach (var line in lines)
        {
            _sessionLogLines.Enqueue(line);
        }

        while (_sessionLogLines.Count > MaxSessionLogLines)
        {
            _sessionLogLines.Dequeue();
        }

        SessionLog = string.Join(Environment.NewLine, _sessionLogLines);
    }

    private void SetSessionLogText(string text)
    {
        _sessionLogLines.Clear();
        if (!string.IsNullOrEmpty(text))
        {
            foreach (var line in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                _sessionLogLines.Enqueue(line);
            }
        }

        SessionLog = text;
    }

    private void ClearLog()
    {
        SetSessionLogText(string.Empty);
        Log.Information("Run log cleared.");
    }

    private static string FormatLogLine(DateTime timestamp, string message) =>
        $"[{timestamp:HH:mm:ss}] {message}";

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

    public async Task<bool> RequestCloseAsync()
    {
        if (!IsBusy)
        {
            Log.Information("Application close requested with no active workflow.");
            return true;
        }

        Log.Information("Application close requested while workflow is active. ActiveOperation={ActiveOperation}", ActiveOperationLabel);
        var confirmed = _confirmationService.Confirm(
            _localization["Confirm.Close.Title"],
            _localization["Confirm.Close.Message"]);

        if (!confirmed)
        {
            Log.Information("Application close declined by user.");
            return false;
        }

        await CancelActiveOperationAsync(_localization["Log.ShutdownRequested"]);
        Log.Information("Application close confirmed after cancelling active workflow.");
        return true;
    }

    private async Task CancelActiveOperationAsync(string reason)
    {
        if (_activeWorkflowCancellation is null)
        {
            return;
        }

        AppendLog(reason);
        _activeWorkflowCancellation.Cancel();

        if (_activeWorkflowTask is not null)
        {
            try
            {
                await _activeWorkflowTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await RefreshCurrentProjectStateAsync(autoDeleteLockIfPresent: true);
    }

    private async Task RefreshCurrentProjectStateAsync(bool autoDeleteLockIfPresent)
    {
        if (_currentProject is null)
        {
            return;
        }

        try
        {
            var refreshedProject = await _workspaceService.OpenAsync(_currentProject.Files.ProjectDirectory);
            ApplyProject(refreshedProject, preserveLog: true, resetDrafts: false);

            if (autoDeleteLockIfPresent && HasLockFile)
            {
                var deleted = await _workspaceService.DeleteLockFileAsync(_currentProject.Files.ProjectDirectory);
                if (deleted)
                {
                    AppendLog(_localization["Log.LockDeletedAfterCancel"]);
                    refreshedProject = await _workspaceService.OpenAsync(_currentProject.Files.ProjectDirectory);
                    ApplyProject(refreshedProject, preserveLog: true, resetDrafts: false);
                }
            }
        }
        catch (Exception exception)
        {
            AppendLog(_localization.Format("Log.ProjectRefreshFailed", exception.Message));
        }
    }

    private async Task ChangeLanguageAsync(AppLanguage language)
    {
        try
        {
            await _userPreferencesService.SetLanguageAsync(language);
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.PreferenceUpdateFailed", exception.Message));
            Log.Error(exception, "Language change failed. Language={Language}", language);
        }
    }

    private async Task ChangeThemeModeAsync(AppThemeMode themeMode)
    {
        try
        {
            await _userPreferencesService.SetThemeModeAsync(themeMode);
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.PreferenceUpdateFailed", exception.Message));
            Log.Error(exception, "Theme mode change failed. ThemeMode={ThemeMode}", themeMode);
        }
    }

    private void OnUserPreferencesChanged(object? sender, EventArgs e)
    {
        RebuildPreferenceOptions();
        ApplyLocalizedShellState();
        Log.Information(
            "User preferences changed. Language={Language} ThemeMode={ThemeMode}",
            _userPreferencesService.CurrentLanguage,
            _userPreferencesService.CurrentThemeMode);
    }

    private void RebuildPreferenceOptions()
    {
        _isSynchronizingPreferenceSelections = true;
        try
        {
            LanguageOptions.Clear();
            LanguageOptions.Add(new SelectionOption<AppLanguage>(AppLanguage.English, _localization["Settings.LanguageEnglish"]));
            LanguageOptions.Add(new SelectionOption<AppLanguage>(AppLanguage.Russian, _localization["Settings.LanguageRussian"]));
            SelectedLanguage = LanguageOptions.First(option => option.Value == _userPreferencesService.CurrentLanguage);

            ThemeModeOptions.Clear();
            ThemeModeOptions.Add(new SelectionOption<AppThemeMode>(AppThemeMode.Light, _localization["Settings.ThemeLight"]));
            ThemeModeOptions.Add(new SelectionOption<AppThemeMode>(AppThemeMode.Dark, _localization["Settings.ThemeDark"]));
            ThemeModeOptions.Add(new SelectionOption<AppThemeMode>(AppThemeMode.Custom, _localization["Settings.ThemeCustom"]));
            SelectedThemeMode = ThemeModeOptions.First(option => option.Value == _userPreferencesService.CurrentThemeMode);
        }
        finally
        {
            _isSynchronizingPreferenceSelections = false;
        }
    }

    private void ApplyLocalizedShellState()
    {
        if (_currentProject is null)
        {
            SelectedProjectPath = _localization["Project.SelectedNone"];
            ProjectStatus = _localization["Project.Status.NoSelection"];
            ProjectName = _localization["Project.Name.None"];
            ProjectKind = _localization["Common.Unknown"];
            ProjectFamily = _localization["Common.Unknown"];
            ActiveProfile = "-";
            Fqbn = "-";
            DiscoveryLabel = _localization["Project.Discovery.Idle"];
            ImportActionLabel = _localization["Import.Action.CreateUss"];
            ImportHint = _localization["Import.Hint.Pending"];
            BootstrapFamily = _localization["Common.Unknown"];
            OpenProjectLocationCommand.NotifyCanExecuteChanged();

            if (string.IsNullOrWhiteSpace(SessionLog))
            {
                SetSessionLogText(_localization["Log.Empty"]);
            }
        }
        else
        {
            ApplyProject(_currentProject, preserveLog: true, resetDrafts: false);
        }

        if (!IsBusy || string.IsNullOrWhiteSpace(ActiveOperationLabel))
        {
            ActiveOperationLabel = ResolveInactiveOperationLabel();
        }

        DiagnosticsSummary = BuildDiagnosticsSummary();
    }

    private string LocalizeProjectFamily(ProjectFamily family) =>
        family switch
        {
            USS.Desktop.Domain.ProjectFamily.Esp32 => _localization["Project.Family.Esp32"],
            USS.Desktop.Domain.ProjectFamily.Stm32 => _localization["Project.Family.Stm32"],
            _ => _localization["Common.Unknown"]
        };

    private string LocalizeProjectKind(string? kind, bool hasSketchConfiguration)
    {
        if (string.Equals(kind, "arduino", StringComparison.OrdinalIgnoreCase) || hasSketchConfiguration)
        {
            return _localization["Project.Kind.Arduino"];
        }

        return string.IsNullOrWhiteSpace(kind) ? _localization["Common.Unknown"] : kind;
    }

    private string LocalizeValidationIssue(ProjectValidationIssue issue)
    {
        var arguments = issue.Arguments ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return issue.Code switch
        {
            "uss.parse" => _localization.Format("Validation.uss.parse", GetArgument(arguments, "error", issue.Message)),
            "sketch.parse" => _localization.Format("Validation.sketch.parse", GetArgument(arguments, "error", issue.Message)),
            "uss.version" => _localization.Format("Validation.uss.version", GetArgument(arguments, "version", issue.Message)),
            "uss.kind" => _localization.Format("Validation.uss.kind", GetArgument(arguments, "kind", issue.Message)),
            "sketch.missing" => _localization["Validation.sketch.missing"],
            "profile.missing" => _localization["Validation.profile.missing"],
            "profile.not-found" => _localization.Format("Validation.profile.not-found", GetArgument(arguments, "profile", issue.Message)),
            "fqbn.missing" => _localization.Format("Validation.fqbn.missing", GetArgument(arguments, "profile", issue.Message)),
            "family.unsupported" => _localization.Format("Validation.family.unsupported", GetArgument(arguments, "fqbn", issue.Message)),
            "family.mismatch" => _localization.Format(
                "Validation.family.mismatch",
                GetArgument(arguments, "declaredFamily", issue.Message),
                GetArgument(arguments, "detectedFamily", issue.Message)),
            "project.locked" => _localization["Validation.project.locked"],
            _ => issue.Message
        };
    }

    private string LocalizeWorkflowAction(string operationKey) =>
        operationKey switch
        {
            CompileOperationKey => _localization["Workflow.Action.Compile"],
            UploadOperationKey => _localization["Workflow.Action.Upload"],
            CompileAndUploadOperationKey => _localization["Workflow.Action.CompileAndUpload"],
            _ => operationKey
        };

    private string LocalizeWorkflowSummary(string operationKey, WorkflowResult result)
    {
        if (result.Success)
        {
            return operationKey switch
            {
                CompileOperationKey => _localization["Workflow.Summary.CompileSuccess"],
                UploadOperationKey => _localization["Workflow.Summary.UploadSuccess"],
                CompileAndUploadOperationKey => _localization["Workflow.Summary.CompileAndUploadSuccess"],
                _ => result.Summary
            };
        }

        if (result.ExitCode is { } exitCode)
        {
            return operationKey switch
            {
                CompileOperationKey => _localization.Format("Workflow.Summary.CompileFailedExit", exitCode),
                UploadOperationKey => _localization.Format("Workflow.Summary.UploadFailedExit", exitCode),
                CompileAndUploadOperationKey => _localization.Format("Workflow.Summary.CompileAndUploadFailedExit", exitCode),
                _ => result.Summary
            };
        }

        return result.Summary switch
        {
            "arduino-cli is not available." => _localization["Workflow.Summary.ToolUnavailable"],
            "The project is already busy." => _localization["Workflow.Summary.ProjectBusy"],
            "A serial port is required for upload." => _localization["Workflow.Summary.PortRequired"],
            "uss.yaml is required." => _localization["Workflow.Summary.UssRequired"],
            "No active sketch profile is available." => _localization["Workflow.Summary.ProfileMissing"],
            "Project validation failed." => _localization["Workflow.Summary.ValidationFailed"],
            _ => result.Summary
        };
    }

    private static string GetArgument(IReadOnlyDictionary<string, string> arguments, string key, string fallback) =>
        arguments.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private void NotifyUploadCommandsChanged()
    {
        UploadCommand.NotifyCanExecuteChanged();
        CompileAndUploadCommand.NotifyCanExecuteChanged();
    }

    private bool IsUploadPortAvailable()
    {
        var normalizedPort = NormalizeSelectedPort(SelectedPort);
        if (IsAutoPort(normalizedPort))
        {
            return _lastPorts.Count > 0;
        }

        if (string.IsNullOrWhiteSpace(normalizedPort))
        {
            return false;
        }

        return _lastPorts.Any(port => string.Equals(port.Address, normalizedPort, StringComparison.OrdinalIgnoreCase));
    }

    private string? GetPortOverrideForWorkflow()
    {
        var normalizedPort = NormalizeSelectedPort(SelectedPort);
        return string.IsNullOrWhiteSpace(normalizedPort)
            ? null
            : normalizedPort;
    }

    private static string NormalizeSelectedPort(string? selectedPort) =>
        selectedPort?.Trim() ?? string.Empty;

    private static bool IsAutoPort(string? selectedPort) =>
        string.Equals(selectedPort, AutoPortSelection, StringComparison.OrdinalIgnoreCase);

    private void OpenProjectLocation()
    {
        if (!CanOpenProjectLocation())
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedProjectPath,
                UseShellExecute = true,
                Verb = "open"
            });

            AppendLog(_localization.Format("Log.ProjectFolderOpened", SelectedProjectPath));
        }
        catch (Exception exception)
        {
            ProjectStatus = exception.Message;
            AppendLog(_localization.Format("Log.ProjectFolderOpenFailed", exception.Message));
            Log.Error(exception, "Failed to open project location. Path={Path}", SelectedProjectPath);
        }
    }

    private void WriteRuntimeLog(string message)
    {
        if (message.StartsWith("CLI OUT |", StringComparison.Ordinal))
        {
            Log.Debug("{RunMessage}", message);
            return;
        }

        if (message.StartsWith("CLI ERR |", StringComparison.Ordinal)
            || message.StartsWith("CLI FAIL |", StringComparison.Ordinal))
        {
            Log.Warning("{RunMessage}", message);
            return;
        }

        if (message.StartsWith("CLI CMD |", StringComparison.Ordinal)
            || message.StartsWith("CLI EXIT |", StringComparison.Ordinal)
            || message.StartsWith("CLI INFO |", StringComparison.Ordinal))
        {
            Log.Information("{RunMessage}", message);
            return;
        }

        Log.Information("{RunMessage}", message);
    }

    private void LogStateSnapshot(string stage)
    {
        Log.Information("{Stage} Snapshot={@State}", stage, BuildStateSnapshot());
    }

    private object BuildStateSnapshot()
    {
        return new
        {
            Settings = _userPreferencesService.CurrentSettings,
            SelectedProjectPath,
            ProjectName,
            ProjectKind,
            ProjectFamily,
            ActiveProfile,
            Fqbn,
            DiscoveryLabel,
            ProjectStatus,
            ActiveOperationLabel,
            HasLockFile,
            LockFilePath,
            ShowImportPanel,
            ShowBootstrapFields,
            IsBusy,
            IsStoppingOperation,
            UseCleanBuild,
            UseVerboseOutput,
            SelectedPort,
            UploadPortAvailable = IsUploadPortAvailable(),
            AvailablePorts = AvailablePorts.ToArray(),
            RecentProjects = RecentProjects.ToArray(),
            Issues = Issues.ToArray(),
            DiagnosticsSummary
        };
    }

    private string ResolveInactiveOperationLabel()
    {
        if (_currentProject is null)
        {
            return _localization["Actions.ActivityNone"];
        }

        return _currentProject.CanCompile
            ? _localization["Actions.ActivityReady"]
            : _localization["Actions.ActivityUnavailable"];
    }

    public void Dispose()
    {
        _userPreferencesService.SettingsChanged -= OnUserPreferencesChanged;

        if (_portPollingTimer is not null)
        {
            _portPollingTimer.Stop();
            _portPollingTimer.Tick -= OnPortPollingTick;
            _portPollingTimer = null;
        }
    }

    private sealed class BatchedLogProgress : IProgress<string>, IDisposable
    {
        private readonly MainViewModel _owner;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _timer;
        private readonly object _gate = new();
        private readonly List<string> _pendingLines = new();
        private bool _disposed;

        public BatchedLogProgress(MainViewModel owner)
        {
            _owner = owner;
            _dispatcher = Dispatcher.CurrentDispatcher;
            _timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = WorkflowLogFlushInterval
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public void Report(string value)
        {
            _owner.WriteRuntimeLog(value);
            var line = FormatLogLine(DateTime.Now, value);

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _pendingLines.Add(line);
            }
        }

        public void Flush()
        {
            var lines = TakePendingLines(markDisposed: false);
            if (lines.Count == 0)
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                _owner.AppendFormattedLogLines(lines);
                return;
            }

            _dispatcher.Invoke(() => _owner.AppendFormattedLogLines(lines));
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;

            var lines = TakePendingLines(markDisposed: true);
            if (lines.Count == 0)
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                _owner.AppendFormattedLogLines(lines);
                return;
            }

            _dispatcher.Invoke(() => _owner.AppendFormattedLogLines(lines));
        }

        private void OnTick(object? sender, EventArgs e)
        {
            Flush();
        }

        private IReadOnlyList<string> TakePendingLines(bool markDisposed)
        {
            lock (_gate)
            {
                if (markDisposed)
                {
                    _disposed = true;
                }

                if (_pendingLines.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var lines = _pendingLines.ToArray();
                _pendingLines.Clear();
                return lines;
            }
        }
    }
}
