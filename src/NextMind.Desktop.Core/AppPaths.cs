namespace NextMind.Desktop.Core;

public static class AppPaths
{
    /// <summary>Development/testing only: redirects config + logs. The guarded filesystem still refuses protected locations.</summary>
    public const string ConfigDirectoryOverrideVariable = "NEXTMIND_DESKTOP_CONFIG_DIR";

    /// <summary>
    /// <c>%LOCALAPPDATA%\NextMind\Desktop</c>, resolved through the known-folder API.
    /// Product config, logs and (later) journals live here — never on the Desktop.
    /// </summary>
    public const string PortableMarkerFileName = "NextMindDesktop.portable";

    public static bool IsPortableMode()
        => File.Exists(Path.Combine(AppContext.BaseDirectory, PortableMarkerFileName));

    public static string ResolveConfigDirectory(string localApplicationData, string? overridePath, bool portableMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);

        return !string.IsNullOrWhiteSpace(overridePath)
            ? Path.GetFullPath(overridePath)
            : Path.Combine(localApplicationData, "NextMind", portableMode ? "Desktop-Portable" : "Desktop");
    }

    public static string ConfigDirectory()
        => ResolveConfigDirectory(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
            Environment.GetEnvironmentVariable(ConfigDirectoryOverrideVariable),
            IsPortableMode());

    public static string LogDirectory() => Path.Combine(ConfigDirectory(), "logs");
}
