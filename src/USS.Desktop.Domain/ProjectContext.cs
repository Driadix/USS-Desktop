using System.IO;

namespace USS.Desktop.Domain;

public sealed record ProjectContext(
    ProjectFiles Files,
    ProjectDiscoveryKind DiscoveryKind,
    UssProjectConfiguration? UssConfiguration,
    SketchConfiguration? SketchConfiguration,
    string? ActiveProfileName,
    SketchProfile? ActiveProfile,
    ProjectFamily Family,
    IReadOnlyList<ProjectValidationIssue> Issues)
{
    public bool HasIssues => Issues.Count > 0;

    public bool IsManagedProject => DiscoveryKind == ProjectDiscoveryKind.ManagedProject;

    public bool CanCreateUssConfiguration => DiscoveryKind == ProjectDiscoveryKind.SketchProjectNeedsImport;

    public bool CanBootstrapSketchProject => DiscoveryKind == ProjectDiscoveryKind.SketchFolderNeedsBootstrap;

    public bool CanCompile => IsManagedProject && ActiveProfile is not null && !HasIssues;

    public string DisplayName =>
        UssConfiguration?.Project.Name
        ?? Path.GetFileName(Files.ProjectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}
