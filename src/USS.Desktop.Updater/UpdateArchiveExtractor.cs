using System.IO.Compression;

namespace USS.Desktop.Updater;

public static class UpdateArchiveExtractor
{
    private const int MaxEntryCount = 10_000;
    private const long MaxUncompressedBytes = 2L * 1024 * 1024 * 1024;

    public static void ExtractToDirectory(string archivePath, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = EnsureTrailingSeparator(Path.GetFullPath(destinationDirectory));
        var entryCount = 0;
        long totalUncompressedBytes = 0;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            entryCount++;
            if (entryCount > MaxEntryCount)
            {
                throw new InvalidDataException("Update package contains too many files.");
            }

            totalUncompressedBytes += entry.Length;
            if (totalUncompressedBytes > MaxUncompressedBytes)
            {
                throw new InvalidDataException("Update package is too large after extraction.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Update package contains an unsafe path.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            entry.ExtractToFile(destinationPath, overwrite: false);
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
