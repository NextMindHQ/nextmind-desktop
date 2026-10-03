namespace NextMind.Desktop.Core;

public static class AppPaths
{
    /// <summary>Development/testing only: redirects config + logs. The guarded filesystem still refuses protected locations.</summary>
    public const string ConfigDirectoryOverrideVariable = "NEXTMIND_DESKTOP_CONFIG_DIR";

    /// <summary>
    /// <c>%LOCALAPPDATA%\NextMind\Desktop</c>, resolved through the known-folder API.
    /// Product config, logs and (later) journals live here — never on the Desktop.
    /// </summary>
    public static string ConfigDirectory()
        => Environment.GetEnvironmentVariable(ConfigDirectoryOverrideVariable) is { Length: > 0 } overridePath
            ? Path.GetFullPath(overridePath)
            : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            "NextMind",
            "Desktop");

    public static string LogDirectory() => Path.Combine(ConfigDirectory(), "logs");
}
