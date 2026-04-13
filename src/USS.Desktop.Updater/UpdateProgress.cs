namespace USS.Desktop.Updater;

public sealed record UpdateProgress(int? Percent, string Message, bool IsError = false);
