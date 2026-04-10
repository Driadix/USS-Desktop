using USS.Desktop.Domain;

namespace USS.Desktop.Application;

public sealed record BootstrapArduinoProjectRequest(
    string FolderPath,
    string ProjectName,
    string ProfileName,
    string Fqbn,
    string PlatformIdentifier,
    string PlatformVersion,
    string? PlatformIndexUrl,
    IReadOnlyList<SketchLibraryReference> Libraries);
