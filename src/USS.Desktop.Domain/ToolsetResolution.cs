namespace USS.Desktop.Domain;

public sealed record ToolsetResolution(
    string ApplicationRoot,
    string? ArduinoCliPath,
    string ArduinoDataDirectory,
    string ArduinoUserDirectory,
    string? FailureMessage)
{
    public bool IsAvailable => !string.IsNullOrWhiteSpace(ArduinoCliPath) && string.IsNullOrWhiteSpace(FailureMessage);
}
