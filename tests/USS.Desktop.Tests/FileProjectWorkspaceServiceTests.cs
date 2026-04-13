using USS.Desktop.Application;
using USS.Desktop.Domain;
using USS.Desktop.Infrastructure;

namespace USS.Desktop.Tests;

public sealed class FileProjectWorkspaceServiceTests
{
    [Fact]
    public async Task OpenAsync_WithManagedProject_LoadsConfigurationAndProfile()
    {
        using var tempDirectory = new TestDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory.Path, "sketch.yaml"),
            """
            default_profile: main
            profiles:
              main:
                fqbn: esp32:esp32:lilygo_t_display_s3
                platforms:
                  - platform: esp32:esp32 (3.3.7)
                    platform_index_url: https://espressif.github.io/arduino-esp32/package_esp32_index.json
                libraries:
                  - TFT_eSPI (2.5.43)
            """);

        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory.Path, "uss.yaml"),
            """
            version: 1
            project:
              name: Radio Test
              kind: arduino
              family: esp32
              profile: main
            artifacts:
              output_dir: build/out
              log_dir: build/logs
              work_dir: build/work
            upload:
              port: auto
              verify_after_upload: true
            """);

        var service = new FileProjectWorkspaceService();
        var project = await service.OpenAsync(tempDirectory.Path);

        Assert.Equal(ProjectDiscoveryKind.ManagedProject, project.DiscoveryKind);
        Assert.Equal("Radio Test", project.DisplayName);
        Assert.Equal(ProjectFamily.Esp32, project.Family);
        Assert.Equal("main", project.ActiveProfileName);
        Assert.NotNull(project.ActiveProfile);
        Assert.Equal("esp32:esp32:lilygo_t_display_s3", project.ActiveProfile!.Fqbn);
        Assert.Empty(project.Issues);
    }

    [Fact]
    public async Task OpenAsync_WithStm32ManagedProject_LoadsConfigurationAndProfile()
    {
        using var tempDirectory = new TestDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory.Path, "sketch.yaml"),
            """
            default_profile: main
            profiles:
              main:
                fqbn: STMicroelectronics:stm32:GenF4
                platforms:
                  - platform: STMicroelectronics:stm32 (2.7.1)
                    platform_index_url: https://github.com/stm32duino/BoardManagerFiles/raw/main/package_stmicroelectronics_index.json
                libraries:
                  - AS5600 (0.4.1)
                  - STM32_CAN (1.1.0)
                  - STM32_TimerInterrupt (1.3.0)
                  - STM32duino RTC (1.6.0)
            """);

        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory.Path, "uss.yaml"),
            """
            version: 1
            project:
              name: STM32 Test
              kind: arduino
              family: stm32
              profile: main
            artifacts:
              output_dir: build/out
              log_dir: build/logs
              work_dir: build/work
            upload:
              port: auto
              verify_after_upload: true
            """);

        var service = new FileProjectWorkspaceService();
        var project = await service.OpenAsync(tempDirectory.Path);

        Assert.Equal(ProjectDiscoveryKind.ManagedProject, project.DiscoveryKind);
        Assert.Equal("STM32 Test", project.DisplayName);
        Assert.Equal(ProjectFamily.Stm32, project.Family);
        Assert.Equal("main", project.ActiveProfileName);
        Assert.NotNull(project.ActiveProfile);
        Assert.Equal("STMicroelectronics:stm32:GenF4", project.ActiveProfile!.Fqbn);
        Assert.Contains(project.ActiveProfile.Platforms, platform => platform.Platform == "STMicroelectronics:stm32" && platform.Version == "2.7.1");
        Assert.Contains(project.ActiveProfile.Libraries, library => library.Name == "STM32_CAN" && library.Version == "1.1.0");
        Assert.Empty(project.Issues);
    }

    [Fact]
    public async Task CreateUssConfigurationAsync_FromSketchOnlyFolder_CreatesManagedProject()
    {
        using var tempDirectory = new TestDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(tempDirectory.Path, "sketch.yaml"),
            """
            default_profile: main
            profiles:
              main:
                fqbn: esp32:esp32:lilygo_t_display_s3
                platforms:
                  - platform: esp32:esp32 (3.3.7)
                libraries:
                  - TFT_eSPI (2.5.43)
            """);

        var service = new FileProjectWorkspaceService();

        var project = await service.CreateUssConfigurationAsync(
            new CreateUssConfigurationRequest(tempDirectory.Path, "Imported Radio"));

        Assert.Equal(ProjectDiscoveryKind.ManagedProject, project.DiscoveryKind);
        Assert.Equal("Imported Radio", project.DisplayName);
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "uss.yaml")));
        Assert.Equal(ProjectFamily.Esp32, project.UssConfiguration!.Project.Family);
    }

    [Fact]
    public async Task BootstrapArduinoProjectAsync_FromSketchFolder_CreatesSketchAndUssFiles()
    {
        using var tempDirectory = new TestDirectory();
        await File.WriteAllTextAsync(Path.Combine(tempDirectory.Path, "radio_test_v2.ino"), "void setup(){} void loop(){}");

        var service = new FileProjectWorkspaceService();

        var project = await service.BootstrapArduinoProjectAsync(
            new BootstrapArduinoProjectRequest(
                tempDirectory.Path,
                "Radio Test",
                "main",
                "esp32:esp32:lilygo_t_display_s3",
                "esp32:esp32",
                "3.3.7",
                "https://espressif.github.io/arduino-esp32/package_esp32_index.json",
                new[]
                {
                    new SketchLibraryReference("TFT_eSPI", "2.5.43")
                }));

        Assert.Equal(ProjectDiscoveryKind.ManagedProject, project.DiscoveryKind);
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "sketch.yaml")));
        Assert.True(File.Exists(Path.Combine(tempDirectory.Path, "uss.yaml")));
        Assert.Equal("Radio Test", project.DisplayName);
        Assert.Equal(ProjectFamily.Esp32, project.Family);
        Assert.Empty(project.Issues);
    }

    [Fact]
    public async Task DeleteLockFileAsync_RemovesExistingLockFile()
    {
        using var tempDirectory = new TestDirectory();
        var lockFilePath = Path.Combine(tempDirectory.Path, "build", "work", "uss.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lockFilePath)!);
        await File.WriteAllTextAsync(lockFilePath, "locked");

        var service = new FileProjectWorkspaceService();
        var deleted = await service.DeleteLockFileAsync(tempDirectory.Path);

        Assert.True(deleted);
        Assert.False(File.Exists(lockFilePath));
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
