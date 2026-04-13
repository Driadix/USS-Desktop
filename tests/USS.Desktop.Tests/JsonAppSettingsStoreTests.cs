using USS.Desktop.Application;
using USS.Desktop.Infrastructure;

namespace USS.Desktop.Tests;

public sealed class JsonAppSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenFileIsMissing_ReturnsDefaultSettings()
    {
        using var tempDirectory = new TestDirectory();
        var store = new JsonAppSettingsStore(Path.Combine(tempDirectory.Path, "app-settings.json"));

        var settings = await store.LoadAsync();

        Assert.Equal(AppSettings.Default, settings);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsLanguageThemeAndPalette()
    {
        using var tempDirectory = new TestDirectory();
        var storagePath = Path.Combine(tempDirectory.Path, "app-settings.json");
        var store = new JsonAppSettingsStore(storagePath);
        var settings = new AppSettings(
            AppLanguage.Russian,
            AppThemeMode.Custom,
            new AppThemePalette(
                "#101820",
                "#1A2733",
                "#223040",
                "#36485E",
                "#F0F2F5",
                "#A4B0BD",
                "#D96A37",
                "#4A92BF",
                "#57A36F",
                "#D2A144",
                "#D95A48",
                "#101418"));

        await store.SaveAsync(settings);
        var reloaded = await store.LoadAsync();

        Assert.Equal(settings, reloaded);
    }

    private sealed class TestDirectory : IDisposable
    {
        public TestDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "uss-desktop-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
