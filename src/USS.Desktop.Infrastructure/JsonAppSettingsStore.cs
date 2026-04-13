using System.IO;
using System.Text.Json;
using USS.Desktop.Application;

namespace USS.Desktop.Infrastructure;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _storagePath;
    private readonly string? _legacyStoragePath;

    public JsonAppSettingsStore(string? storagePath = null, string? legacyStoragePath = null)
    {
        _storagePath = storagePath ?? AppDataPaths.SettingsFilePath();
        _legacyStoragePath = legacyStoragePath ?? (storagePath is null
            ? Path.Combine(AppContext.BaseDirectory, "uss-data", "app-settings.json")
            : null);
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath))
        {
            return await LoadLegacySettingsAsync(cancellationToken);
        }

        try
        {
            await using var stream = File.OpenRead(_storagePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            return settings ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }

    private async Task<AppSettings> LoadLegacySettingsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_legacyStoragePath) || !File.Exists(_legacyStoragePath))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_legacyStoragePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken);
            if (settings is null)
            {
                return AppSettings.Default;
            }

            await SaveAsync(settings, cancellationToken);
            return settings;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }
}
