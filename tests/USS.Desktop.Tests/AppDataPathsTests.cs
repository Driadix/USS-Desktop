using USS.Desktop.Application;

namespace USS.Desktop.Tests;

public sealed class AppDataPathsTests
{
    [Fact]
    public void ResolveDefaultRoot_UsesLocalApplicationData()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = AppDataPaths.ResolveDefaultRoot();

        Assert.StartsWith(localApplicationData, root, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("USSDesktop", root, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(' ', AppDataPaths.ProductDirectoryName);
    }

    [Fact]
    public void ResolveDefaultToolsetDataRoot_UsesShortUserProfilePath()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var root = AppDataPaths.ResolveDefaultToolsetDataRoot();

        Assert.Equal(Path.Combine(userProfile, ".uss"), root);
    }

    [Fact]
    public void SettingsFilePath_UsesExplicitRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "uss-desktop-tests", Guid.NewGuid().ToString("N"));

        var settingsPath = AppDataPaths.SettingsFilePath(root);

        Assert.Equal(Path.Combine(root, "app-settings.json"), settingsPath);
    }

    [Fact]
    public void ResolveToolsetDataRoot_UsesExplicitRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "uss-desktop-tests", Guid.NewGuid().ToString("N"));

        var toolsetDataRoot = AppDataPaths.ResolveToolsetDataRoot(root);

        Assert.Equal(root, toolsetDataRoot);
    }
}
