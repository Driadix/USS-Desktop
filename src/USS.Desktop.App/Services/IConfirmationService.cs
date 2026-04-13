namespace USS.Desktop.App.Services;

public interface IConfirmationService
{
    bool Confirm(string title, string message);

    void ShowInformation(string title, string message);
}
