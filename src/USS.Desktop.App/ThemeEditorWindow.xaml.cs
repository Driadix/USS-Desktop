using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using USS.Desktop.Application;
using USS.Desktop.App.Services;
using MessageBox = System.Windows.MessageBox;
using MediaBrush = System.Windows.Media.Brush;

namespace USS.Desktop.App;

public partial class ThemeEditorWindow : Window
{
    private readonly LocalizationService _localizationService;

    public ThemeEditorWindow(LocalizationService localizationService, AppThemePalette initialPalette)
    {
        InitializeComponent();
        _localizationService = localizationService;
        DataContext = this;

        WindowTitle = localizationService["ThemeEditor.Title"];
        DescriptionText = localizationService["ThemeEditor.Description"];
        PickButtonText = localizationService["ThemeEditor.Pick"];
        ResetLightButtonText = localizationService["ThemeEditor.ResetLight"];
        ResetDarkButtonText = localizationService["ThemeEditor.ResetDark"];
        SaveButtonText = localizationService["ThemeEditor.Save"];
        CancelButtonText = localizationService["ThemeEditor.Cancel"];

        Entries = new ObservableCollection<ThemeColorEntry>(CreateEntries(initialPalette));
    }

    public ObservableCollection<ThemeColorEntry> Entries { get; }

    public string WindowTitle { get; }

    public string DescriptionText { get; }

    public string PickButtonText { get; }

    public string ResetLightButtonText { get; }

    public string ResetDarkButtonText { get; }

    public string SaveButtonText { get; }

    public string CancelButtonText { get; }

    public AppThemePalette? SelectedPalette { get; private set; }

    private void OnPickColorClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ThemeColorEntry entry)
        {
            return;
        }

        using var dialog = new ColorDialog
        {
            FullOpen = true,
            AllowFullOpen = true,
            AnyColor = true,
            Color = ResolveDialogColor(entry.HexValue)
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            entry.HexValue = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        }
    }

    private void OnResetLightClick(object sender, RoutedEventArgs e)
    {
        ApplyPalette(AppThemePalette.LightDefault);
    }

    private void OnResetDarkClick(object sender, RoutedEventArgs e)
    {
        ApplyPalette(AppThemePalette.DarkDefault);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            SelectedPalette = new AppThemePalette(
                ReadEntry("PageBackground"),
                ReadEntry("ChromeBackground"),
                ReadEntry("CardBackground"),
                ReadEntry("BorderColor"),
                ReadEntry("TextColor"),
                ReadEntry("MutedTextColor"),
                ReadEntry("PrimaryColor"),
                ReadEntry("AccentColor"),
                ReadEntry("SuccessColor"),
                ReadEntry("WarningColor"),
                ReadEntry("DangerColor"),
                ReadEntry("LogBackground"));

            DialogResult = true;
        }
        catch (FormatException exception)
        {
            MessageBox.Show(
                string.Format(_localizationService["ThemeEditor.InvalidColorMessage"], exception.Message),
                _localizationService["ThemeEditor.InvalidColorTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ApplyPalette(AppThemePalette palette)
    {
        foreach (var entry in CreateEntries(palette))
        {
            var existingEntry = Entries.First(currentEntry => string.Equals(currentEntry.Key, entry.Key, StringComparison.Ordinal));
            existingEntry.HexValue = entry.HexValue;
        }
    }

    private string ReadEntry(string key)
    {
        var entry = Entries.First(currentEntry => string.Equals(currentEntry.Key, key, StringComparison.Ordinal));
        return ThemeColorHelper.NormalizeHex(entry.HexValue);
    }

    private static System.Drawing.Color ResolveDialogColor(string hexValue)
    {
        try
        {
            return System.Drawing.ColorTranslator.FromHtml(ThemeColorHelper.NormalizeHex(hexValue));
        }
        catch (FormatException)
        {
            return System.Drawing.Color.White;
        }
    }

    private IReadOnlyList<ThemeColorEntry> CreateEntries(AppThemePalette palette)
    {
        return new[]
        {
            new ThemeColorEntry("PageBackground", _localizationService["ThemeEditor.PageBackground"], palette.PageBackground),
            new ThemeColorEntry("ChromeBackground", _localizationService["ThemeEditor.ChromeBackground"], palette.ChromeBackground),
            new ThemeColorEntry("CardBackground", _localizationService["ThemeEditor.CardBackground"], palette.CardBackground),
            new ThemeColorEntry("BorderColor", _localizationService["ThemeEditor.BorderColor"], palette.BorderColor),
            new ThemeColorEntry("TextColor", _localizationService["ThemeEditor.TextColor"], palette.TextColor),
            new ThemeColorEntry("MutedTextColor", _localizationService["ThemeEditor.MutedTextColor"], palette.MutedTextColor),
            new ThemeColorEntry("PrimaryColor", _localizationService["ThemeEditor.PrimaryColor"], palette.PrimaryColor),
            new ThemeColorEntry("AccentColor", _localizationService["ThemeEditor.AccentColor"], palette.AccentColor),
            new ThemeColorEntry("SuccessColor", _localizationService["ThemeEditor.SuccessColor"], palette.SuccessColor),
            new ThemeColorEntry("WarningColor", _localizationService["ThemeEditor.WarningColor"], palette.WarningColor),
            new ThemeColorEntry("DangerColor", _localizationService["ThemeEditor.DangerColor"], palette.DangerColor),
            new ThemeColorEntry("LogBackground", _localizationService["ThemeEditor.LogBackground"], palette.LogBackground)
        };
    }

    public sealed partial class ThemeColorEntry : ObservableObject
    {
        public ThemeColorEntry(string key, string displayName, string hexValue)
        {
            Key = key;
            DisplayName = displayName;
            _hexValue = ThemeColorHelper.NormalizeHex(hexValue);
            _previewBrush = new SolidColorBrush(ThemeColorHelper.ParseHex(_hexValue));
        }

        public string Key { get; }

        public string DisplayName { get; }

        [ObservableProperty]
        private string _hexValue;

        [ObservableProperty]
        private MediaBrush _previewBrush;

        partial void OnHexValueChanged(string value)
        {
            try
            {
                PreviewBrush = new SolidColorBrush(ThemeColorHelper.ParseHex(value));
            }
            catch (FormatException)
            {
            }
        }
    }
}
