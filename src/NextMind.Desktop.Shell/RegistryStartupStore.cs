using Microsoft.Win32;
using NextMind.Desktop.Core.Startup;

namespace NextMind.Desktop.Shell;

/// <summary>
/// "Start with Windows" via the per-user Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run): documented,
/// no admin, no service, no scheduled task; runs only after the user logs on. Enable/disable = write/remove one value.
/// </summary>
public sealed class RegistryStartupStore(string keyPath = RegistryStartupStore.RunKeyPath, string valueName = RegistryStartupStore.ProductValueName) : IStartupStore
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ProductValueName = "NextMindDesktop";

    public string KeyPath { get; } = keyPath;

    public string ValueName { get; } = valueName;

    public string? Read()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value switch
        {
            null => null,
            string s => s,
            _ => string.Empty, // wrong value type: treated as a broken entry
        };
    }

    public void Write(string commandLine)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(ValueName, commandLine, RegistryValueKind.String);
    }

    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
