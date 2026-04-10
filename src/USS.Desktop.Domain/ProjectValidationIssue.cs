namespace USS.Desktop.Domain;

public sealed record ProjectValidationIssue(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string>? Arguments = null);
