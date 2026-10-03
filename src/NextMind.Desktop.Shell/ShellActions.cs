using System.ComponentModel;
using System.Diagnostics;

namespace NextMind.Desktop.Shell;

/// <summary>
/// Opening things goes through the Windows Shell (ShellExecute), exactly like double-clicking in Explorer:
/// folder -> Explorer, file -> default app, .lnk -> runs the shortcut, .exe -> starts the program.
/// Nothing here reads, moves, copies or deletes the target.
/// </summary>
public static class ShellActions
{
    /// <summary>True when the path is an existing directory (read-only probe).</summary>
    public static bool IsDirectory(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    public static bool TryOpen(string path, out string? error)
    {
        error = null;
        try
        {
            var info = new ProcessStartInfo(path) { UseShellExecute = true };

            // Like Explorer, start programs in their own folder.
            if (File.Exists(path) && Path.GetDirectoryName(path) is { Length: > 0 } dir && Directory.Exists(dir))
            {
                info.WorkingDirectory = dir;
            }

            Process.Start(info)?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Opens Explorer with the item selected; if it is gone, opens its nearest existing parent folder.</summary>
    public static bool TryShowInExplorer(string path, out string? error)
    {
        error = null;
        try
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = false, Arguments = $"/select,\"{path}\"" })?.Dispose();
                return true;
            }

            var parent = Path.GetDirectoryName(path);
            while (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent))
            {
                parent = Path.GetDirectoryName(parent);
            }

            if (string.IsNullOrEmpty(parent))
            {
                error = "Neither the item nor its folder exists.";
                return false;
            }

            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = false, Arguments = $"\"{parent}\"" })?.Dispose();
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }
}
