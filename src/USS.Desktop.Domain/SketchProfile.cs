namespace USS.Desktop.Domain;

public sealed record SketchProfile(
    string Name,
    string? Notes,
    string Fqbn,
    IReadOnlyList<SketchPlatformReference> Platforms,
    IReadOnlyList<SketchLibraryReference> Libraries,
    string? Port,
    string? Protocol);
