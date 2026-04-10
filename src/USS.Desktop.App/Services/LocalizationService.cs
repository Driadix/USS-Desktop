using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using USS.Desktop.Application;

namespace USS.Desktop.App.Services;

public sealed class LocalizationService : ObservableObject
{
    private readonly UserPreferencesService _userPreferencesService;
    private readonly IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> _catalog;

    public LocalizationService(UserPreferencesService userPreferencesService)
    {
        _userPreferencesService = userPreferencesService;
        _catalog = LocalizationCatalog.Build();
        ApplyCulture(_userPreferencesService.CurrentLanguage);
        _userPreferencesService.SettingsChanged += (_, _) =>
        {
            ApplyCulture(_userPreferencesService.CurrentLanguage);
            OnPropertyChanged(nameof(CurrentLanguage));
            OnPropertyChanged("Item[]");
        };
    }

    public AppLanguage CurrentLanguage => _userPreferencesService.CurrentLanguage;

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (_catalog.TryGetValue(CurrentLanguage, out var languageCatalog)
            && languageCatalog.TryGetValue(key, out var localizedValue))
        {
            return localizedValue;
        }

        return _catalog[AppLanguage.English].TryGetValue(key, out var fallback)
            ? fallback
            : key;
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    private static void ApplyCulture(AppLanguage language)
    {
        var culture = language switch
        {
            AppLanguage.Russian => CultureInfo.GetCultureInfo("ru-RU"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
