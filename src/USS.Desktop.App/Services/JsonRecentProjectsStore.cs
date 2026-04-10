using System.IO;
using System.Text.Json;

namespace USS.Desktop.App.Services;

public sealed class JsonRecentProjectsStore : IRecentProjectsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;

    public JsonRecentProjectsStore()
    {
        _storagePath = Path.Combine(AppContext.BaseDirectory, "uss-data", "recent-projects.json");
    }

    public async Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath))
        {
            return Array.Empty<string>();
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
}
