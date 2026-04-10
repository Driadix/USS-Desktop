using USS.Desktop.Domain;

namespace USS.Desktop.Application;

public interface IToolsetResolver
{
    Task<ToolsetResolution> ResolveAsync(CancellationToken cancellationToken = default);
}
