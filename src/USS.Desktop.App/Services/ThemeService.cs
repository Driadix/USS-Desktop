using System.Windows;
using System.Windows.Media;
using Serilog;
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
        var isDarkTheme = ThemeColorHelper.GetLuminance(page) < 0.5d;
        var secondary = ThemeColorHelper.Blend(card, page, isDarkTheme ? 0.18 : 0.36);
        var primaryHover = ThemeColorHelper.ShiftForInteraction(primary, 0.08);
        var primaryPressed = ThemeColorHelper.ShiftForInteraction(primary, 0.16);
        var secondaryHover = ThemeColorHelper.ShiftForInteraction(secondary, 0.06);
        var secondaryPressed = ThemeColorHelper.ShiftForInteraction(secondary, 0.12);
        var dangerHover = ThemeColorHelper.ShiftForInteraction(danger, 0.08);
        var dangerPressed = ThemeColorHelper.ShiftForInteraction(danger, 0.16);
        var chromeSurface = ThemeColorHelper.Blend(chrome, page, isDarkTheme ? 0.12 : 0.16);
        var focus = ThemeColorHelper.Blend(primary, accent, 0.38);
        var scrollTrack = ThemeColorHelper.Blend(page, card, isDarkTheme ? 0.28 : 0.52);
        var scrollThumb = ThemeColorHelper.Blend(accent, border, 0.42);
        var statusActive = ThemeColorHelper.Blend(primary, accent, 0.45);
        var statusIdle = ThemeColorHelper.Blend(card, page, isDarkTheme ? 0.28 : 0.16);

        SetBrush("PageBrush", page);
        SetBrush("ChromeBrush", chrome);
        SetBrush("ChromeSurfaceBrush", chromeSurface);
        SetBrush("ChromeStrokeBrush", ThemeColorHelper.Blend(chrome, page, isDarkTheme ? 0.28 : 0.46));
        SetBrush("CardBrush", card);
        SetBrush("CardAltBrush", ThemeColorHelper.Blend(card, page, 0.24));
        SetBrush("CardStrokeBrush", border);
        SetBrush("InkBrush", text);
        SetBrush("MutedInkBrush", mutedText);
        SetBrush("PrimaryBrush", primary);
        SetBrush("PrimaryHoverBrush", primaryHover);
        SetBrush("PrimaryDarkBrush", primaryPressed);
        SetBrush("PrimaryTextBrush", ThemeColorHelper.GetReadableForeground(primary));
        SetBrush("SecondaryBrush", secondary);
        SetBrush("SecondaryHoverBrush", secondaryHover);
        SetBrush("SecondaryPressedBrush", secondaryPressed);
        SetBrush("SecondaryTextBrush", text);
        SetBrush("AccentBrush", accent);
        SetBrush("AccentTextBrush", ThemeColorHelper.GetReadableForeground(accent));
        SetBrush("SuccessBrush", success);
        SetBrush("WarningBrush", warning);
        SetBrush("WarningSurfaceBrush", ThemeColorHelper.Blend(warning, card, 0.84));
        SetBrush("DangerBrush", danger);
        SetBrush("DangerHoverBrush", dangerHover);
        SetBrush("DangerDarkBrush", dangerPressed);
        SetBrush("DangerTextBrush", ThemeColorHelper.GetReadableForeground(danger));
        SetBrush("DangerSurfaceBrush", ThemeColorHelper.Blend(danger, card, 0.86));
        SetBrush("InputFocusBrush", focus);
        SetBrush("ScrollTrackBrush", scrollTrack);
        SetBrush("ScrollThumbBrush", scrollThumb);
        SetBrush("ScrollThumbHoverBrush", ThemeColorHelper.ShiftForInteraction(scrollThumb, 0.08));
        SetBrush("LogBackgroundBrush", logBackground);
        SetBrush("LogBorderBrush", ThemeColorHelper.Blend(border, logBackground, 0.3));
        SetBrush("OverlayArrowBrush", ThemeColorHelper.WithOpacity(primary, 0.82));
        SetBrush("OverlayArrowHoverBrush", primaryHover);
        SetBrush("StatusActiveBrush", statusActive);
        SetBrush("StatusIdleBrush", statusIdle);

        Log.Information(
            "Theme applied. Mode={ThemeMode} IsDarkTheme={IsDarkTheme} Palette={@Palette} OpenWindowCount={OpenWindowCount}",
            _userPreferencesService.CurrentThemeMode,
            isDarkTheme,
            palette,
            WpfApplication.Current.Windows.Count);
    }

    private static void SetBrush(string key, MediaColor color)
    {
        if (WpfApplication.Current.Resources[key] is SolidColorBrush brush)
        {
            if (!brush.IsFrozen)
            {
                brush.Color = color;
                return;
            }

            WpfApplication.Current.Resources[key] = new SolidColorBrush(color);
            return;
        }

        WpfApplication.Current.Resources[key] = new SolidColorBrush(color);
    }
}
