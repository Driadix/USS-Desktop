using System.IO;
using USS.Desktop.Application;
using USS.Desktop.Domain;

namespace USS.Desktop.Infrastructure;

public sealed class ArduinoCliToolsetResolver : IToolsetResolver
{
    public Task<ToolsetResolution> ResolveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var applicationRoot = ResolveApplicationRoot();
        var dataDirectory = Path.Combine(applicationRoot, "uss-data", "arduino-cli", "data");
        var userDirectory = Path.Combine(applicationRoot, "uss-data", "arduino-cli", "user");

        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(userDirectory);

        var cliPath = FindArduinoCliPath(applicationRoot);
        var failureMessage = cliPath is null
            ? "arduino-cli.exe was not found. Put it under toolsets/arduino-cli*/ or install it into PATH."
            : null;

        return Task.FromResult(new ToolsetResolution(applicationRoot, cliPath, dataDirectory, userDirectory, failureMessage));
    }

    private static string ResolveApplicationRoot()
    {
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        foreach (var candidate in EnumerateAncestorDirectories(baseDirectory))
        {
            if (File.Exists(Path.Combine(candidate, "USS.Desktop.slnx")) || Directory.Exists(Path.Combine(candidate, "toolsets")))
            {
                return candidate;
            }
        }

        return baseDirectory;
    }

    private static string? FindArduinoCliPath(string applicationRoot)
    {
        foreach (var candidateRoot in GetSearchRoots(applicationRoot))
        {
            var directPath = Path.Combine(candidateRoot, "toolsets", "arduino-cli.exe");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            var toolsetsDirectory = Path.Combine(candidateRoot, "toolsets");
            if (!Directory.Exists(toolsetsDirectory))
            {
                continue;
            }

            var nestedMatch = Directory
                .EnumerateFiles(toolsetsDirectory, "arduino-cli.exe", SearchOption.AllDirectories)
                .OrderByDescending(path => path.Count(character => character == Path.DirectorySeparatorChar))
                .FirstOrDefault();

            if (nestedMatch is not null)
            {
                return nestedMatch;
            }
        }

        return FindOnPath();
    }

    private static IEnumerable<string> GetSearchRoots(string applicationRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in EnumerateAncestorDirectories(applicationRoot))
        {
            if (seen.Add(root))
            {
                yield return root;
            }
        }

        var currentDirectory = Path.GetFullPath(Environment.CurrentDirectory);
        foreach (var root in EnumerateAncestorDirectories(currentDirectory))
        {
            if (seen.Add(root))
            {
                yield return root;
            }
        }
    }

    private static IEnumerable<string> EnumerateAncestorDirectories(string startPath)
    {
        var current = startPath;
        while (!string.IsNullOrWhiteSpace(current))
        {
            yield return current;

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                yield break;
            }

            current = parent.FullName;
        }
    }

    private static string? FindOnPath()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        foreach (var segment in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(segment, "arduino-cli.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
