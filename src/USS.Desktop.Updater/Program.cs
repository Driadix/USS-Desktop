using System.Diagnostics;
using System.Net.Http.Headers;
using System.Windows.Forms;
using USS.Desktop.Updater;
using WinFormsApplication = System.Windows.Forms.Application;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinFormsApplication.EnableVisualStyles();
        WinFormsApplication.SetCompatibleTextRenderingDefault(false);

        using var form = new UpdaterProgressForm(args);
        WinFormsApplication.Run(form);
        return form.ExitCode;
    }
}

internal static class UpdaterProgram
{
    private const int ProcessExitTimeoutMilliseconds = 120_000;

    public static async Task<int> RunAsync(
        string[] args,
        IProgress<UpdateProgress>? progress = null,
        TimeSpan? restartDelay = null)
    {
        var options = UpdaterOptions.Parse(args);
        if (!options.IsValid(out var validationMessage))
        {
            await UpdateLog.WriteAsync($"Invalid updater arguments: {validationMessage}");
            progress?.Report(new UpdateProgress(100, "Update could not start. Opening the release page...", IsError: true));
            var releasePageOpened = await OpenReleasePageAsync(options.ReleasePageUrl);
            progress?.Report(new UpdateProgress(100, CreateFailureMessage(releasePageOpened), IsError: true));
            return 2;
        }

        var workingRoot = string.Empty;
        try
        {
            progress?.Report(new UpdateProgress(null, "Waiting for USS Desktop to close..."));
            await WaitForApplicationExitAsync(options.ProcessId);

            progress?.Report(new UpdateProgress(20, "Preparing update..."));
            workingRoot = Path.Combine(Path.GetTempPath(), "USS.Desktop.Update", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingRoot);

            var archivePath = Path.Combine(workingRoot, "update.zip");
            var stagingPath = Path.Combine(workingRoot, "staging");

            await DownloadAsync(options.DownloadUrl!, archivePath, progress);

            progress?.Report(new UpdateProgress(65, "Verifying update package..."));
            if (!UpdateDigestVerifier.VerifyFile(archivePath, options.Sha256Digest))
            {
                throw new InvalidOperationException("Downloaded update package failed SHA-256 verification.");
            }

            progress?.Report(new UpdateProgress(75, "Extracting update package..."));
            UpdateArchiveExtractor.ExtractToDirectory(archivePath, stagingPath);

            progress?.Report(new UpdateProgress(88, "Installing update..."));
            UpdatePackageInstaller.Install(stagingPath, options.AppDirectory!, options.ExecutableName!);

            TryDeleteDirectory(workingRoot);
            await UpdateLog.WriteAsync("Update completed.");

            progress?.Report(new UpdateProgress(100, "Update installed successfully. Opening USS Desktop..."));
            var delay = restartDelay.GetValueOrDefault();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
            }

            RestartApplication(options.AppDirectory!, options.ExecutableName!);
            return 0;
        }
        catch (Exception exception)
        {
            await UpdateLog.WriteAsync("Update failed.", exception);
            progress?.Report(new UpdateProgress(100, "Update failed. Opening the release page...", IsError: true));
            var releasePageOpened = await OpenReleasePageAsync(options.ReleasePageUrl);
            progress?.Report(new UpdateProgress(100, CreateFailureMessage(releasePageOpened), IsError: true));
            TryDeleteDirectory(workingRoot);
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

    private static async Task DownloadAsync(
        Uri downloadUrl,
        string archivePath,
        IProgress<UpdateProgress>? progress)
    {
        progress?.Report(new UpdateProgress(null, "Downloading update..."));

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("USS-Desktop-Updater", "1.0"));

        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        await using var archiveStream = File.Create(archivePath);

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength is null or <= 0)
        {
            await responseStream.CopyToAsync(archiveStream);
            return;
        }

        var buffer = new byte[81920];
        long totalRead = 0;
        var lastPercent = -1;
        while (true)
        {
            var bytesRead = await responseStream.ReadAsync(buffer);
            if (bytesRead == 0)
            {
                return;
            }

            await archiveStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;

            var percent = 30 + (int)Math.Round(totalRead * 30D / contentLength.Value);
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(new UpdateProgress(Math.Clamp(percent, 30, 60), "Downloading update..."));
            }
        }
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

    private static string CreateFailureMessage(bool releasePageOpened) =>
        releasePageOpened
            ? "Update failed. Download the latest version manually. The release page was opened in your browser."
            : "Update failed. Download the latest version manually from the USS Desktop releases page.";

    private static async Task<bool> OpenReleasePageAsync(Uri? releasePageUrl)
    {
        if (releasePageUrl is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = releasePageUrl.AbsoluteUri,
                UseShellExecute = true
            });

            return true;
        }
        catch (Exception exception)
        {
            await UpdateLog.WriteAsync("Could not open release page.", exception);
            return false;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }
}
