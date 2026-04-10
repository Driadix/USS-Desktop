using System.Windows;
using WpfApplication = System.Windows.Application;

namespace USS.Desktop.App.Services;

public sealed class ThemeEditorDialogService : IThemeEditorDialogService
{
    private readonly LocalizationService _localizationService;
    private readonly ThemeService _themeService;
    private readonly UserPreferencesService _userPreferencesService;

    public ThemeEditorDialogService(
        LocalizationService localizationService,
        ThemeService themeService,
        UserPreferencesService userPreferencesService)
    {
        _localizationService = localizationService;
        _themeService = themeService;
        _userPreferencesService = userPreferencesService;
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        var window = new ThemeEditorWindow(_localizationService, _themeService.GetEffectivePalette())
        {
            Owner = WpfApplication.Current.Windows.OfType<Window>().FirstOrDefault(currentWindow => currentWindow.IsActive)
                ?? WpfApplication.Current.MainWindow
        };

        if (window.ShowDialog() == true && window.SelectedPalette is not null)
        {
            await _userPreferencesService.SetCustomThemeAsync(window.SelectedPalette, cancellationToken);
        }
    }
}
