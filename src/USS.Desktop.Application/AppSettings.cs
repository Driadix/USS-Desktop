namespace USS.Desktop.Application;

public sealed record AppSettings(
    AppLanguage Language,
    AppThemeMode ThemeMode,
    AppThemePalette CustomTheme)
{
    public static AppSettings Default { get; } =
        new(AppLanguage.English, AppThemeMode.Light, AppThemePalette.LightDefault);
}
