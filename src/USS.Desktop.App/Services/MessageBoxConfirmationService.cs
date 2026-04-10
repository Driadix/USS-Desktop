using System.Windows;

namespace USS.Desktop.App.Services;

public sealed class MessageBoxConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message) =>
        System.Windows.MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
}
