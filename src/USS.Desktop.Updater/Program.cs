using System.Diagnostics;
using System.Net.Http.Headers;
using USS.Desktop.Application;
using USS.Desktop.Updater;

return await UpdaterProgram.RunAsync(args);

internal static class UpdaterProgram
{
    private const int ProcessExitTimeoutMilliseconds = 120_000;

    public static async Task<int> RunAsync(string[] args)
    {
        var options = UpdaterOptions.Parse(args);
        if (!options.IsValid(out var validationMessage))
        {
            await UpdateLog.WriteAsync($"Invalid updater arguments: {validationMessage}");
            return 2;
        }

        try
        {
            await WaitForApplicationExitAsync(options.ProcessId);

            var workingRoot = Path.Combine(Path.GetTempPath(), "USS.Desktop.Update", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingRoot);

            var archivePath = Path.Combine(workingRoot, "update.zip");
            var stagingPath = Path.Combine(workingRoot, "staging");
            await DownloadAsync(options.DownloadUrl!, archivePath);

            if (!UpdateDigestVerifier.VerifyFile(archivePath, options.Sha256Digest))
            {
                throw new InvalidOperationException("Downloaded update package failed SHA-256 verification.");
            }

            UpdateArchiveExtractor.ExtractToDirectory(archivePath, stagingPath);
            UpdatePackageInstaller.Install(stagingPath, options.AppDirectory!, options.ExecutableName!);
            RestartApplication(options.AppDirectory!, options.ExecutableName!);

            TryDeleteDirectory(workingRoot);
            await UpdateLog.WriteAsync("Update completed.");
            return 0;
        }
        catch (Exception exception)
        {
            await UpdateLog.WriteAsync("Update failed.", exception);
            return 1;
        }
    }

    private static async Task WaitForApplicationExitAsync(int? processId)
    {
        if (processId is null)
        {
            return;
        }

        try
        {
            using var process = Process.GetProcessById(processId.Value);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(ProcessExitTimeoutMilliseconds));
        }
        catch (ArgumentException)
        {
        }
        catch (TimeoutException)
        {
            throw new TimeoutException("Timed out waiting for USS Desktop to exit.");
        }
    }

    private static async Task DownloadAsync(Uri downloadUrl, string archivePath)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("USS-Desktop-Updater", "1.0"));

        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        await using var archiveStream = File.Create(archivePath);
        await responseStream.CopyToAsync(archiveStream);
    }

    private static void RestartApplication(string appDirectory, string executableName)
    {
        var executablePath = Path.Combine(appDirectory, executableName);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Updated application executable was not found.", executablePath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = appDirectory,
            UseShellExecute = true
        });
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
