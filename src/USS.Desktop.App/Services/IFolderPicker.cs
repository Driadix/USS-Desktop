namespace USS.Desktop.App.Services;

public interface IFolderPicker
{
    string? PickFolder(string? initialPath = null);
}
