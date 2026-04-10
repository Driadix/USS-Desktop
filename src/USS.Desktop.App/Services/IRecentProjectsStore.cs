namespace USS.Desktop.App.Services;

public interface IRecentProjectsStore
{
    Task<IReadOnlyList<string>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<string> projects, CancellationToken cancellationToken = default);
}
