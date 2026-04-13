namespace USS.Desktop.App.Services;

public interface IApplicationVersionProvider
{
    Version CurrentVersion { get; }

    string DisplayVersion { get; }
}
