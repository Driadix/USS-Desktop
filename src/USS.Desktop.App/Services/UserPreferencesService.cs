using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using USS.Desktop.Application;

namespace USS.Desktop.App.Services;

public sealed class UserPreferencesService : ObservableObject
{
    private readonly IAppSettingsStore _appSettingsStore;
    private AppSettings _currentSettings = AppSettings.Default;

    public UserPreferencesService(IAppSettingsStore appSettingsStore)
    {
        _appSettingsStore = appSettingsStore;
    }

    public AppSettings CurrentSettings => _currentSettings;

    public AppLanguage CurrentLanguage => _currentSettings.Language;

    public AppThemeMode CurrentThemeMode => _currentSettings.ThemeMode;

    public AppThemePalette CurrentCustomTheme => _currentSettings.CustomTheme;

    public event EventHandler? SettingsChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _currentSettings = await _appSettingsStore.LoadAsync(cancellationToken);
        Log.Information("User preferences initialized. Settings={@Settings}", _currentSettings);
        RaiseStateChanged();
    }

    public Task SetLanguageAsync(AppLanguage language, CancellationToken cancellationToken = default)
    {
        if (_currentSettings.Language == language)
        {
            return Task.CompletedTask;
        }

        return SaveAsync(_currentSettings with { Language = language }, cancellationToken);
    }

    public Task SetThemeModeAsync(AppThemeMode themeMode, CancellationToken cancellationToken = default)
    {
        if (_currentSettings.ThemeMode == themeMode)
        {
            return Task.CompletedTask;
        }

        return SaveAsync(_currentSettings with { ThemeMode = themeMode }, cancellationToken);
    }

    public Task SetCustomThemeAsync(AppThemePalette palette, CancellationToken cancellationToken = default) =>
        SaveAsync(_currentSettings with
        {
            ThemeMode = AppThemeMode.Custom,
            CustomTheme = palette
        }, cancellationToken);

    private async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var previousSettings = _currentSettings;
        _currentSettings = settings;
        RaiseStateChanged();

        try
        {
            await _appSettingsStore.SaveAsync(settings, cancellationToken);
            Log.Information("User preferences saved. Settings={@Settings}", settings);
        }
        catch (Exception exception)
        {
            _currentSettings = previousSettings;
            RaiseStateChanged();
            Log.Error(exception, "Failed to save user preferences.");
            throw;
        }
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(CurrentSettings));
        OnPropertyChanged(nameof(CurrentLanguage));
        OnPropertyChanged(nameof(CurrentThemeMode));
        OnPropertyChanged(nameof(CurrentCustomTheme));
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
