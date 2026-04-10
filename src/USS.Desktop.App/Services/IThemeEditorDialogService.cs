namespace USS.Desktop.App.Services;

public interface IThemeEditorDialogService
{
    Task OpenAsync(CancellationToken cancellationToken = default);
}
