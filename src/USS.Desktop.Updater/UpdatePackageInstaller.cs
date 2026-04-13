namespace USS.Desktop.Updater;

public static class UpdatePackageInstaller
{
    public static void Install(string packageRoot, string appDirectory, string executableName)
    {
        var updateRoot = ResolveUpdateRoot(packageRoot, executableName);
        var fullAppDirectory = Path.GetFullPath(appDirectory);
        var backupRoot = Path.Combine(Path.GetTempPath(), "USS.Desktop.UpdateBackup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backupRoot);

        try
        {
            MoveCurrentApplicationToBackup(fullAppDirectory, backupRoot);
            CopyDirectory(updateRoot, fullAppDirectory);

            var updatedExecutable = Path.Combine(fullAppDirectory, executableName);
            if (!File.Exists(updatedExecutable))
            {
                throw new FileNotFoundException("The update package did not install the application executable.", updatedExecutable);
            }

            TryDeleteDirectory(backupRoot);
        }
        catch
        {
            RestoreBackup(fullAppDirectory, backupRoot);
            throw;
        }
    }

    private static string ResolveUpdateRoot(string packageRoot, string executableName)
    {
        var fullPackageRoot = Path.GetFullPath(packageRoot);
        var directExecutable = Path.Combine(fullPackageRoot, executableName);
        if (File.Exists(directExecutable))
        {
            return fullPackageRoot;
        }

        var nestedRoot = Directory
            .EnumerateFiles(fullPackageRoot, executableName, SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        if (nestedRoot is null)
        {
            throw new FileNotFoundException("The update package does not contain the application executable.", directExecutable);
        }

        return nestedRoot;
    }

    private static void MoveCurrentApplicationToBackup(string appDirectory, string backupRoot)
    {
        foreach (var fileSystemInfo in Directory.EnumerateFileSystemEntries(appDirectory))
        {
            var name = Path.GetFileName(fileSystemInfo);
            var backupPath = Path.Combine(backupRoot, name);
            if (Directory.Exists(fileSystemInfo))
            {
                Directory.Move(fileSystemInfo, backupPath);
            }
            else
            {
                File.Move(fileSystemInfo, backupPath);
            }
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            var destinationDirectoryPath = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectoryPath))
            {
                Directory.CreateDirectory(destinationDirectoryPath);
            }

            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static void RestoreBackup(string appDirectory, string backupRoot)
    {
        try
        {
            DeleteReplaceableEntries(appDirectory);
            CopyDirectory(backupRoot, appDirectory);
            TryDeleteDirectory(backupRoot);
        }
        catch
        {
        }
    }

    private static void DeleteReplaceableEntries(string appDirectory)
    {
        foreach (var fileSystemInfo in Directory.EnumerateFileSystemEntries(appDirectory))
        {
            if (Directory.Exists(fileSystemInfo))
            {
                Directory.Delete(fileSystemInfo, recursive: true);
            }
            else
            {
                File.Delete(fileSystemInfo);
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
