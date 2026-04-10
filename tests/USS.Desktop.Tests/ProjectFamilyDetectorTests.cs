using USS.Desktop.Domain;

namespace USS.Desktop.Tests;

public sealed class ProjectFamilyDetectorTests
{
    [Theory]
    [InlineData("esp32:esp32:lilygo_t_display_s3", ProjectFamily.Esp32)]
    [InlineData("stm32:stm32:Nucleo_64", ProjectFamily.Stm32)]
    [InlineData("arduino:avr:uno", ProjectFamily.Unknown)]
    [InlineData("", ProjectFamily.Unknown)]
    public void FromFqbn_ReturnsExpectedFamily(string fqbn, ProjectFamily expectedFamily)
    {
        var family = ProjectFamilyDetector.FromFqbn(fqbn);
        Assert.Equal(expectedFamily, family);
    }
}
