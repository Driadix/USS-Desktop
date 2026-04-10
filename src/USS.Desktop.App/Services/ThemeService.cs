using System.Windows;
using System.Windows.Media;
using USS.Desktop.Application;
using MediaColor = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace USS.Desktop.App.Services;

public sealed class ThemeService
{
    private readonly UserPreferencesService _userPreferencesService;

    public ThemeService(UserPreferencesService userPreferencesService)
    {
        _userPreferencesService = userPreferencesService;
        _userPreferencesService.SettingsChanged += (_, _) => ApplyCurrentTheme();
    }

    public AppThemePalette GetEffectivePalette()
    {
        return _userPreferencesService.CurrentThemeMode switch
        {
            AppThemeMode.Dark => AppThemePalette.DarkDefault,
            AppThemeMode.Custom => _userPreferencesService.CurrentCustomTheme,
            _ => AppThemePalette.LightDefault
        };
    }

    public void ApplyCurrentTheme()
    {
        var palette = GetEffectivePalette();
        var page = ThemeColorHelper.ParseHex(palette.PageBackground);
        var chrome = ThemeColorHelper.ParseHex(palette.ChromeBackground);
        var card = ThemeColorHelper.ParseHex(palette.CardBackground);
        var border = ThemeColorHelper.ParseHex(palette.BorderColor);
        var text = ThemeColorHelper.ParseHex(palette.TextColor);
        var mutedText = ThemeColorHelper.ParseHex(palette.MutedTextColor);
        var primary = ThemeColorHelper.ParseHex(palette.PrimaryColor);
        var accent = ThemeColorHelper.ParseHex(palette.AccentColor);
        var success = ThemeColorHelper.ParseHex(palette.SuccessColor);
        var warning = ThemeColorHelper.ParseHex(palette.WarningColor);
        var danger = ThemeColorHelper.ParseHex(palette.DangerColor);
        var logBackground = ThemeColorHelper.ParseHex(palette.LogBackground);

        SetBrush("PageBrush", page);
        SetBrush("ChromeBrush", chrome);
        SetBrush("ChromeSurfaceBrush", ThemeColorHelper.Blend(chrome, page, 0.16));
        SetBrush("CardBrush", card);
        SetBrush("CardAltBrush", ThemeColorHelper.Blend(card, page, 0.24));
        SetBrush("CardStrokeBrush", border);
        SetBrush("InkBrush", text);
        SetBrush("MutedInkBrush", mutedText);
        SetBrush("PrimaryBrush", primary);
        SetBrush("PrimaryDarkBrush", ThemeColorHelper.ShiftBrightness(primary, -0.18));
        SetBrush("PrimaryTextBrush", ThemeColorHelper.GetReadableForeground(primary));
        SetBrush("SecondaryBrush", ThemeColorHelper.Blend(card, page, 0.36));
        SetBrush("SecondaryHoverBrush", ThemeColorHelper.Blend(card, page, 0.22));
        SetBrush("SecondaryTextBrush", text);
        SetBrush("AccentBrush", accent);
        SetBrush("AccentTextBrush", ThemeColorHelper.GetReadableForeground(accent));
        SetBrush("SuccessBrush", success);
        SetBrush("WarningBrush", warning);
        SetBrush("WarningSurfaceBrush", ThemeColorHelper.Blend(warning, card, 0.84));
        SetBrush("DangerBrush", danger);
        SetBrush("DangerDarkBrush", ThemeColorHelper.ShiftBrightness(danger, -0.18));
        SetBrush("DangerTextBrush", ThemeColorHelper.GetReadableForeground(danger));
        SetBrush("DangerSurfaceBrush", ThemeColorHelper.Blend(danger, card, 0.86));
        SetBrush("LogBackgroundBrush", logBackground);
        SetBrush("LogBorderBrush", ThemeColorHelper.Blend(border, logBackground, 0.3));
        SetBrush("OverlayArrowBrush", ThemeColorHelper.WithOpacity(primary, 0.76));
        SetBrush("OverlayArrowHoverBrush", primary);
    }

    private static void SetBrush(string key, MediaColor color)
    {
        if (WpfApplication.Current.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        WpfApplication.Current.Resources[key] = new SolidColorBrush(color);
    }
}
