namespace USS.Desktop.Domain;

public enum ProjectFamily
{
    Unknown = 0,
    Esp32 = 1,
    Stm32 = 2
}

public static class ProjectFamilyDetector
{
    public static ProjectFamily FromFqbn(string? fqbn)
    {
        if (string.IsNullOrWhiteSpace(fqbn))
        {
            return ProjectFamily.Unknown;
        }

        var segments = fqbn.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return ProjectFamily.Unknown;
        }

        return $"{segments[0]}:{segments[1]}".ToLowerInvariant() switch
        {
            "esp32:esp32" => ProjectFamily.Esp32,
            "stmicroelectronics:stm32" => ProjectFamily.Stm32,
            "stm32:stm32" => ProjectFamily.Stm32,
            _ => ProjectFamily.Unknown
        };
    }

    public static string ToDisplayName(this ProjectFamily family) =>
        family switch
        {
            ProjectFamily.Esp32 => "ESP32 Arduino",
            ProjectFamily.Stm32 => "STM32 Arduino",
            _ => "Unknown"
        };
}
