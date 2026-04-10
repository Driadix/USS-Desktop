namespace USS.Desktop.Domain;

public sealed record ProjectFiles(
    string ProjectDirectory,
    string? UssFilePath,
    string? SketchFilePath,
    string? PrimarySketchPath);
