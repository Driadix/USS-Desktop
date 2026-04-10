using System.Windows.Forms;

namespace USS.Desktop.App.Services;

public sealed class FolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialPath = null)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a USS project folder",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            InitialDirectory = string.IsNullOrWhiteSpace(initialPath) ? Environment.CurrentDirectory : initialPath
        };

        return dialog.ShowDialog() == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
