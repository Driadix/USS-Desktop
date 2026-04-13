using System.IO;
using System.Text.Json;
using USS.Desktop.Application;

namespace USS.Desktop.App.Services;

public sealed class JsonRecentProjectsStore : IRecentProjectsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;
    private readonly string? _legacyStoragePath;

    public JsonRecentProjectsStore(string? storagePath = null, string? legacyStoragePath = null)
    {
        _storagePath = storagePath ?? AppDataPaths.RecentProjectsFilePath();
        _legacyStoragePath = legacyStoragePath ?? (storagePath is null
            ? Path.Combine(AppContext.BaseDirectory, "uss-data", "recent-projects.json")
            : null);
    }

    public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath))
        {
            return await LoadLegacyProjectsAsync(cancellationToken);
        }

        await using var stream = File.OpenRead(_storagePath);
        var projects = await JsonSerializer.DeserializeAsync<List<string>>(stream, SerializerOptions, cancellationToken);
        return projects is null ? Array.Empty<string>() : projects;
    }

    public async Task SaveAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, projects, SerializerOptions, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> LoadLegacyProjectsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_legacyStoragePath) || !File.Exists(_legacyStoragePath))
        {
            return Array.Empty<string>();
        }

        await using var stream = File.OpenRead(_legacyStoragePath);
        var projects = await JsonSerializer.DeserializeAsync<List<string>>(stream, SerializerOptions, cancellationToken);
        if (projects is null)
        {
            return Array.Empty<string>();
        }

        await SaveAsync(projects, cancellationToken);
        return projects;
    }
}
