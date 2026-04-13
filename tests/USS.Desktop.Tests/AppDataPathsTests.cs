using USS.Desktop.Application;

namespace USS.Desktop.Tests;

public sealed class AppDataPathsTests
{
    [Fact]
    public void ResolveDefaultRoot_UsesLocalApplicationData()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = AppDataPaths.ResolveDefaultRoot();

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            Assert.Contains("uss-data", root, StringComparison.OrdinalIgnoreCase);
            return;
        }

        Assert.StartsWith(localApplicationData, root, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(AppDataPaths.ProductDirectoryName, root, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsFilePath_UsesExplicitRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "uss-desktop-tests", Guid.NewGuid().ToString("N"));

        var settingsPath = AppDataPaths.SettingsFilePath(root);

        Assert.Equal(Path.Combine(root, "app-settings.json"), settingsPath);
    }
}
