namespace USS.Desktop.Domain;

public sealed record SketchConfiguration(
    string? DefaultProfile,
    IReadOnlyDictionary<string, SketchProfile> Profiles);
