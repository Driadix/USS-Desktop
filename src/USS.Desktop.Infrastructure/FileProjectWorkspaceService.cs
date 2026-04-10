using System.IO;
using USS.Desktop.Application;
using USS.Desktop.Domain;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace USS.Desktop.Infrastructure;

public sealed class FileProjectWorkspaceService : IProjectWorkspaceService
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public FileProjectWorkspaceService()
    {
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        _serializer = new SerializerBuilder()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    public async Task<ProjectContext> OpenAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("A project folder path is required.", nameof(folderPath));
        }

        var fullPath = Path.GetFullPath(folderPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Project folder '{fullPath}' does not exist.");
        }

        var issues = new List<ProjectValidationIssue>();
        var ussPath = Path.Combine(fullPath, "uss.yaml");
        var sketchPath = Path.Combine(fullPath, "sketch.yaml");
        var primarySketchPath = FindPrimarySketchPath(fullPath);

        var ussConfiguration = await TryReadUssAsync(ussPath, issues, cancellationToken);
        var sketchConfiguration = await TryReadSketchAsync(sketchPath, issues, cancellationToken);
        var discoveryKind = DetermineDiscoveryKind(ussConfiguration is not null, sketchConfiguration is not null, primarySketchPath is not null);
        var activeProfileName = ResolveActiveProfileName(ussConfiguration, sketchConfiguration);
        var activeProfile = ResolveActiveProfile(sketchConfiguration, activeProfileName);
        var family = activeProfile is not null
            ? ProjectFamilyDetector.FromFqbn(activeProfile.Fqbn)
            : ussConfiguration?.Project.Family ?? ProjectFamily.Unknown;

        issues.AddRange(Validate(fullPath, discoveryKind, ussConfiguration, sketchConfiguration, activeProfileName, activeProfile));

        return new ProjectContext(
            new ProjectFiles(fullPath, File.Exists(ussPath) ? ussPath : null, File.Exists(sketchPath) ? sketchPath : null, primarySketchPath),
            discoveryKind,
            ussConfiguration,
            sketchConfiguration,
            activeProfileName,
            activeProfile,
            family,
            issues);
    }

    public async Task<ProjectContext> CreateUssConfigurationAsync(
        CreateUssConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await OpenAsync(request.FolderPath, cancellationToken);
        if (project.SketchConfiguration is null)
        {
            throw new InvalidOperationException("sketch.yaml is required before USS configuration can be created.");
        }

        if (project.UssConfiguration is not null)
        {
            return project;
        }

        if (project.ActiveProfileName is null || project.ActiveProfile is null)
        {
            throw new InvalidOperationException("The active sketch profile could not be resolved.");
        }

        var projectName = string.IsNullOrWhiteSpace(request.ProjectName)
            ? project.DisplayName
            : request.ProjectName.Trim();

        var configuration = ProjectDefaults.CreateUssConfiguration(projectName, project.Family, project.ActiveProfileName);
        var destinationPath = Path.Combine(project.Files.ProjectDirectory, "uss.yaml");
        await WriteUssAsync(destinationPath, configuration, cancellationToken);
        return await OpenAsync(project.Files.ProjectDirectory, cancellationToken);
    }

    public async Task<ProjectContext> BootstrapArduinoProjectAsync(
        BootstrapArduinoProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            throw new InvalidOperationException("Project name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ProfileName))
        {
            throw new InvalidOperationException("Profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Fqbn))
        {
            throw new InvalidOperationException("FQBN is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PlatformIdentifier))
        {
            throw new InvalidOperationException("A pinned platform identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PlatformVersion))
        {
            throw new InvalidOperationException("A pinned platform version is required.");
        }

        var fullPath = Path.GetFullPath(request.FolderPath);
        Directory.CreateDirectory(fullPath);

        var sketchPath = Path.Combine(fullPath, "sketch.yaml");
        var ussPath = Path.Combine(fullPath, "uss.yaml");

        if (File.Exists(sketchPath))
        {
            throw new InvalidOperationException("sketch.yaml already exists in this folder.");
        }

        var family = ProjectFamilyDetector.FromFqbn(request.Fqbn);
        var trimmedProfile = request.ProfileName.Trim();
        var sketchConfiguration = new SketchConfiguration(
            trimmedProfile,
            new Dictionary<string, SketchProfile>(StringComparer.OrdinalIgnoreCase)
            {
                [trimmedProfile] = new SketchProfile(
                    trimmedProfile,
                    "Created by USS Desktop import bootstrap.",
                    request.Fqbn.Trim(),
                    new[]
                    {
                        new SketchPlatformReference(
                            request.PlatformIdentifier.Trim(),
                            request.PlatformVersion.Trim(),
                            PinnedResourceParser.NullIfWhiteSpace(request.PlatformIndexUrl))
                    },
                    request.Libraries,
                    null,
                    null)
            });

        var ussConfiguration = ProjectDefaults.CreateUssConfiguration(request.ProjectName.Trim(), family, trimmedProfile);

        await WriteSketchAsync(sketchPath, sketchConfiguration, cancellationToken);
        await WriteUssAsync(ussPath, ussConfiguration, cancellationToken);
        return await OpenAsync(fullPath, cancellationToken);
    }

    public Task<bool> DeleteLockFileAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(folderPath);
        var lockFilePath = GetLockFilePath(fullPath);
        if (!File.Exists(lockFilePath))
        {
            return Task.FromResult(false);
        }

        File.Delete(lockFilePath);
        return Task.FromResult(true);
    }

    private async Task<UssProjectConfiguration?> TryReadUssAsync(
        string ussPath,
        ICollection<ProjectValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(ussPath))
        {
            return null;
        }

        try
        {
            return await ReadUssAsync(ussPath, cancellationToken);
        }
        catch (Exception exception)
        {
            issues.Add(new ProjectValidationIssue("uss.parse", $"uss.yaml could not be parsed: {exception.Message}"));
            return null;
        }
    }

    private async Task<SketchConfiguration?> TryReadSketchAsync(
        string sketchPath,
        ICollection<ProjectValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(sketchPath))
        {
            return null;
        }

        try
        {
            return await ReadSketchAsync(sketchPath, cancellationToken);
        }
        catch (Exception exception)
        {
            issues.Add(new ProjectValidationIssue("sketch.parse", $"sketch.yaml could not be parsed: {exception.Message}"));
            return null;
        }
    }

    private async Task<UssProjectConfiguration> ReadUssAsync(string path, CancellationToken cancellationToken)
    {
        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        var document = _deserializer.Deserialize<UssYamlDocument>(yaml) ?? new UssYamlDocument();
        var family = ParseFamily(document.Project?.Family);

        return new UssProjectConfiguration(
            document.Version,
            new ProjectMetadata(
                document.Project?.Name?.Trim() ?? Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty),
                document.Project?.Kind?.Trim() ?? "arduino",
                family,
                document.Project?.Profile?.Trim() ?? "main"),
            new ArtifactLayout(
                document.Artifacts?.OutputDir?.Trim() ?? "build/out",
                document.Artifacts?.LogDir?.Trim() ?? "build/logs",
                document.Artifacts?.WorkDir?.Trim() ?? "build/work"),
            new UploadPreferences(
                document.Upload?.Port?.Trim() ?? "auto",
                document.Upload?.VerifyAfterUpload ?? true));
    }

    private async Task<SketchConfiguration> ReadSketchAsync(string path, CancellationToken cancellationToken)
    {
        var yaml = await File.ReadAllTextAsync(path, cancellationToken);
        var document = _deserializer.Deserialize<SketchYamlDocument>(yaml) ?? new SketchYamlDocument();
        var profiles = new Dictionary<string, SketchProfile>(StringComparer.OrdinalIgnoreCase);

        if (document.Profiles is not null)
        {
            foreach (var profile in document.Profiles)
            {
                profiles[profile.Key] = new SketchProfile(
                    profile.Key,
                    PinnedResourceParser.NullIfWhiteSpace(profile.Value.Notes),
                    profile.Value.Fqbn?.Trim() ?? string.Empty,
                    profile.Value.Platforms?.Select(PinnedResourceParser.ParsePlatform).ToArray() ?? Array.Empty<SketchPlatformReference>(),
                    profile.Value.Libraries?.Select(PinnedResourceParser.ParseLibrary).ToArray() ?? Array.Empty<SketchLibraryReference>(),
                    PinnedResourceParser.NullIfWhiteSpace(profile.Value.Port),
                    PinnedResourceParser.NullIfWhiteSpace(profile.Value.Protocol));
            }
        }

        return new SketchConfiguration(PinnedResourceParser.NullIfWhiteSpace(document.DefaultProfile), profiles);
    }

    private async Task WriteUssAsync(string path, UssProjectConfiguration configuration, CancellationToken cancellationToken)
    {
        var document = new UssYamlDocument
        {
            Version = configuration.Version,
            Project = new UssProjectYaml
            {
                Name = configuration.Project.Name,
                Kind = configuration.Project.Kind,
                Family = configuration.Project.Family switch
                {
                    ProjectFamily.Esp32 => "esp32",
                    ProjectFamily.Stm32 => "stm32",
                    _ => "unknown"
                },
                Profile = configuration.Project.Profile
            },
            Artifacts = new ArtifactLayoutYaml
            {
                OutputDir = configuration.Artifacts.OutputDirectory,
                LogDir = configuration.Artifacts.LogDirectory,
                WorkDir = configuration.Artifacts.WorkDirectory
            },
            Upload = new UploadYaml
            {
                Port = configuration.Upload.Port,
                VerifyAfterUpload = configuration.Upload.VerifyAfterUpload
            }
        };

        var yaml = _serializer.Serialize(document);
        await File.WriteAllTextAsync(path, yaml, cancellationToken);
    }

    private async Task WriteSketchAsync(string path, SketchConfiguration configuration, CancellationToken cancellationToken)
    {
        var profiles = new Dictionary<string, SketchProfileYaml>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in configuration.Profiles)
        {
            profiles[profile.Key] = new SketchProfileYaml
            {
                Notes = profile.Value.Notes,
                Fqbn = profile.Value.Fqbn,
                Platforms = profile.Value.Platforms.Select(PinnedResourceParser.FormatPlatform).ToList(),
                Libraries = profile.Value.Libraries.Select(PinnedResourceParser.FormatLibrary).ToList(),
                Port = profile.Value.Port,
                Protocol = profile.Value.Protocol
            };
        }

        var yaml = _serializer.Serialize(new SketchYamlDocument
        {
            DefaultProfile = configuration.DefaultProfile,
            Profiles = profiles
        });

        await File.WriteAllTextAsync(path, yaml, cancellationToken);
    }

    private static ProjectDiscoveryKind DetermineDiscoveryKind(bool hasUss, bool hasSketch, bool hasIno) =>
        (hasUss, hasSketch, hasIno) switch
        {
            (true, _, _) => ProjectDiscoveryKind.ManagedProject,
            (false, true, _) => ProjectDiscoveryKind.SketchProjectNeedsImport,
            (false, false, true) => ProjectDiscoveryKind.SketchFolderNeedsBootstrap,
            _ => ProjectDiscoveryKind.Unsupported
        };

    private static string? ResolveActiveProfileName(
        UssProjectConfiguration? ussConfiguration,
        SketchConfiguration? sketchConfiguration)
    {
        if (!string.IsNullOrWhiteSpace(ussConfiguration?.Project.Profile))
        {
            return ussConfiguration.Project.Profile;
        }

        if (!string.IsNullOrWhiteSpace(sketchConfiguration?.DefaultProfile))
        {
            return sketchConfiguration.DefaultProfile;
        }

        return sketchConfiguration?.Profiles.Count == 1
            ? sketchConfiguration.Profiles.Keys.Single()
            : null;
    }

    private static SketchProfile? ResolveActiveProfile(SketchConfiguration? sketchConfiguration, string? activeProfileName)
    {
        if (sketchConfiguration is null || string.IsNullOrWhiteSpace(activeProfileName))
        {
            return null;
        }

        return sketchConfiguration.Profiles.TryGetValue(activeProfileName, out var activeProfile)
            ? activeProfile
            : null;
    }

    private static string? FindPrimarySketchPath(string folderPath)
    {
        var inoFiles = Directory.EnumerateFiles(folderPath, "*.ino", SearchOption.TopDirectoryOnly).ToArray();
        if (inoFiles.Length == 0)
        {
            return null;
        }

        var folderName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return inoFiles.FirstOrDefault(file => string.Equals(Path.GetFileNameWithoutExtension(file), folderName, StringComparison.OrdinalIgnoreCase))
            ?? inoFiles.OrderBy(Path.GetFileName).First();
    }

    private static ProjectFamily ParseFamily(string? family) =>
        family?.Trim().ToLowerInvariant() switch
        {
            "esp32" => ProjectFamily.Esp32,
            "stm32" => ProjectFamily.Stm32,
            _ => ProjectFamily.Unknown
        };

    private static IReadOnlyList<ProjectValidationIssue> Validate(
        string projectDirectory,
        ProjectDiscoveryKind discoveryKind,
        UssProjectConfiguration? ussConfiguration,
        SketchConfiguration? sketchConfiguration,
        string? activeProfileName,
        SketchProfile? activeProfile)
    {
        var issues = new List<ProjectValidationIssue>();

        if (ussConfiguration is not null)
        {
            if (ussConfiguration.Version != 1)
            {
                issues.Add(new ProjectValidationIssue("uss.version", $"Unsupported uss.yaml version '{ussConfiguration.Version}'."));
            }

            if (!string.Equals(ussConfiguration.Project.Kind, "arduino", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ProjectValidationIssue("uss.kind", $"Unsupported project kind '{ussConfiguration.Project.Kind}'."));
            }
        }

        if (discoveryKind == ProjectDiscoveryKind.ManagedProject && sketchConfiguration is null)
        {
            issues.Add(new ProjectValidationIssue("sketch.missing", "Managed USS project is missing sketch.yaml."));
        }

        if (sketchConfiguration is not null && string.IsNullOrWhiteSpace(activeProfileName))
        {
            issues.Add(new ProjectValidationIssue("profile.missing", "No active sketch profile could be resolved."));
        }

        if (sketchConfiguration is not null && activeProfileName is not null && activeProfile is null)
        {
            issues.Add(new ProjectValidationIssue("profile.not-found", $"Profile '{activeProfileName}' does not exist in sketch.yaml."));
        }

        if (activeProfile is not null)
        {
            if (string.IsNullOrWhiteSpace(activeProfile.Fqbn))
            {
                issues.Add(new ProjectValidationIssue("fqbn.missing", $"Profile '{activeProfile.Name}' is missing an fqbn."));
            }
            else
            {
                var inferredFamily = ProjectFamilyDetector.FromFqbn(activeProfile.Fqbn);
                if (inferredFamily == ProjectFamily.Unknown)
                {
                    issues.Add(new ProjectValidationIssue("family.unsupported", $"FQBN '{activeProfile.Fqbn}' is not supported in v1."));
                }

                if (ussConfiguration is not null
                    && ussConfiguration.Project.Family != ProjectFamily.Unknown
                    && inferredFamily != ProjectFamily.Unknown
                    && inferredFamily != ussConfiguration.Project.Family)
                {
                    issues.Add(new ProjectValidationIssue(
                        "family.mismatch",
                        $"uss.yaml declares family '{ussConfiguration.Project.Family.ToDisplayName()}' but sketch.yaml resolves to '{inferredFamily.ToDisplayName()}'."));
                }
            }
        }

        var lockFilePath = GetLockFilePath(projectDirectory);
        if (File.Exists(lockFilePath))
        {
            issues.Add(new ProjectValidationIssue("project.locked", "The project has an active USS lock file in build/work/uss.lock."));
        }

        return issues;
    }

    private static string GetLockFilePath(string projectDirectory) =>
        Path.Combine(projectDirectory, "build", "work", "uss.lock");
}
