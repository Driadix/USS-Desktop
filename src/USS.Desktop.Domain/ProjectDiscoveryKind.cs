namespace USS.Desktop.Domain;

public enum ProjectDiscoveryKind
{
    Unsupported = 0,
    ManagedProject = 1,
    SketchProjectNeedsImport = 2,
    SketchFolderNeedsBootstrap = 3
}
