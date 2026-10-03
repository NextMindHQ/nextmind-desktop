using NextMind.Desktop.Core.IO;

namespace NextMind.Desktop.Core.Startup;

/// <summary>Where the "start with Windows" command line is kept (per-user HKCU Run value in production).</summary>
public interface IStartupStore
{
    /// <summary>The stored command line, or null if the entry does not exist. A value of the wrong type reads as an empty string.</summary>
    string? Read();

    void Write(string commandLine);

    /// <summary>Removes the entry; a missing entry is not an error.</summary>
    void Remove();
}

public enum StartupState
{
    /// <summary>No entry: the app will not start at logon.</summary>
    Disabled,

    /// <summary>Entry points at this very executable.</summary>
    Enabled,

    /// <summary>Entry exists and its target exists, but it is a different executable (moved/rebuilt copy).</summary>
    EnabledForOtherPath,

    /// <summary>Entry exists but is empty/malformed or its target is gone.</summary>
    Broken,
}

public enum StartupSyncResult
{
    Unchanged,
    Created,
    Repaired,
    Removed,
}

/// <summary>
/// Enable / Disable / Verify for "Start with Windows". All operations are idempotent and touch only the one
/// per-user entry; no service, scheduled task, shell extension or admin rights are involved.
/// </summary>
public sealed class StartupManager(IStartupStore store, IPathProbe probe)
{
    public static string BuildCommand(string exePath) => "\"" + PathUtil.Normalize(exePath) + "\"";

    /// <summary>Extracts the executable path from a Run-value command line (quoted or unquoted), or null if there is none.</summary>
    public static string? TryParseExecutable(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var text = commandLine.Trim();
        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe >= 0 ? text[..(exe + 4)] : null;
    }

    public StartupState GetState(string currentExePath)
    {
        var raw = store.Read();
        if (raw is null)
        {
            return StartupState.Disabled;
        }

        var target = TryParseExecutable(raw);
        if (target is null || !Path.IsPathFullyQualified(target) || !probe.Exists(target))
        {
            return StartupState.Broken;
        }

        return string.Equals(PathUtil.Normalize(target), PathUtil.Normalize(currentExePath), StringComparison.OrdinalIgnoreCase)
            ? StartupState.Enabled
            : StartupState.EnabledForOtherPath;
    }

    /// <summary>Registers this executable. Returns true if the entry was written, false if it was already correct.</summary>
    public bool Enable(string currentExePath)
    {
        if (GetState(currentExePath) == StartupState.Enabled)
        {
            return false;
        }

        store.Write(BuildCommand(currentExePath));
        return true;
    }

    /// <summary>Removes the entry. Returns true if something was removed, false if it was already absent.</summary>
    public bool Disable()
    {
        if (store.Read() is null)
        {
            return false;
        }

        store.Remove();
        return true;
    }

    /// <summary>
    /// Makes the Run entry match the user's preference (the config is the source of truth). Idempotent.
    /// desired=true: a missing, stale (other path) or broken entry is (re)written. desired=false: an existing entry is removed, a missing one is left alone.
    /// </summary>
    public StartupSyncResult Sync(string currentExePath, bool desired)
    {
        var state = GetState(currentExePath);
        if (desired)
        {
            if (state == StartupState.Enabled)
            {
                return StartupSyncResult.Unchanged;
            }

            store.Write(BuildCommand(currentExePath));
            return state == StartupState.Disabled ? StartupSyncResult.Created : StartupSyncResult.Repaired;
        }

        if (state == StartupState.Disabled)
        {
            return StartupSyncResult.Unchanged;
        }

        store.Remove();
        return StartupSyncResult.Removed;
    }

    /// <summary>
    /// If an entry exists but is stale (other path) or broken, points it at the running executable.
    /// A disabled entry is left alone. Returns true when it rewrote the entry.
    /// </summary>
    public bool Reconcile(string currentExePath)
    {
        var state = GetState(currentExePath);
        if (state is StartupState.EnabledForOtherPath or StartupState.Broken)
        {
            store.Write(BuildCommand(currentExePath));
            return true;
        }

        return false;
    }
}
