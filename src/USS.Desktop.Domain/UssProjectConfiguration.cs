namespace USS.Desktop.Domain;

public sealed record UssProjectConfiguration(
    int Version,
    ProjectMetadata Project,
    ArtifactLayout Artifacts,
    UploadPreferences Upload);
