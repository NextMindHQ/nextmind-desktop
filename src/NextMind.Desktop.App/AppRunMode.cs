namespace NextMind.Desktop.App;

public readonly record struct AppRunModeState(bool IsPortable, bool IsDevelopment)
{
    public bool IsIsolated => IsPortable || IsDevelopment;
}

public static class AppRunMode
{
    public static AppRunModeState Resolve(bool portableMode, string? configDirectoryOverride)
        => new(
            portableMode,
            !string.IsNullOrWhiteSpace(configDirectoryOverride));
}
