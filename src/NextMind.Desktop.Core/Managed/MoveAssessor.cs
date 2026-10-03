using System.Diagnostics;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;

namespace NextMind.Desktop.Core.Managed;

public enum MoveVerdict
{
    /// <summary>Safe to move without asking (a shortcut, a small ordinary file, a small clean folder).</summary>
    Allow,

    /// <summary>Technically possible but needs the user's decision (large, project-like, program file, unknown size).</summary>
    Confirm,

    /// <summary>Refused: moving it could break something or its behaviour is unknown. The caller may offer a reference instead.</summary>
    Reject,
}

public enum MoveReason
{
    NotFound,
    ReparsePoint,
    ContainsReparsePoint,
    OfflineOrCloudFile,
    SystemOrHidden,
    InvalidName,
    InsideManagedStorage,
    Executable,
    LargeFile,
    LargeFolder,
    ProjectFolder,
    TooManyEntries,
    SizeUnknown,
}

public sealed record MoveAssessment(
    MoveVerdict Verdict,
    IReadOnlyList<MoveReason> Reasons,
    bool IsDirectory,
    long ApproxBytes,
    int EntryCount,
    bool Truncated,
    string Message);

public sealed class MoveAssessmentOptions
{
    /// <summary>Ordinary files up to this size move without asking.</summary>
    public long SmallFileBytes { get; init; } = 50L * 1024 * 1024;

    /// <summary>Folders at or above this size need confirmation.</summary>
    public long LargeFolderBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>Folders with more entries than this need confirmation.</summary>
    public int ConfirmEntryCount { get; init; } = 5_000;

    /// <summary>The scan stops after this many entries (the folder is then treated as "size unknown").</summary>
    public int MaxEntriesScanned { get; init; } = 20_000;

    /// <summary>The scan stops after this much time.</summary>
    public TimeSpan Budget { get; init; } = TimeSpan.FromMilliseconds(1500);

    /// <summary>Folder names that suggest a development project or a dependency/build tree.</summary>
    public IReadOnlyList<string> ProjectMarkers { get; init; } =
        [".git", "node_modules", ".venv", "venv", "bin", "obj", ".vs", "__pycache__", ".idea", ".svn", ".hg"];
}

/// <summary>
/// Decides whether a Desktop item may be moved into managed storage. Fail-closed whitelist:
/// reparse points (symlinks, junctions, cloud placeholders) and system/hidden items are refused; big, project-like or
/// program items need an explicit user decision; shortcuts, small ordinary files and small clean folders go through.
/// Read-only and bounded (entry cap + time budget); callers run it off the UI thread.
/// </summary>
public sealed class MoveAssessor(IFileSystem fs, string managedRoot, MoveAssessmentOptions? options = null)
{
    private const FileAttributes OfflineFlags = (FileAttributes)(0x1000 | 0x40000 | 0x400000); // OFFLINE | RECALL_ON_OPEN | RECALL_ON_DATA_ACCESS

    private static readonly HashSet<string> ShortcutExtensions = new(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url", ".website" };

    private static readonly HashSet<string> ProgramExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".com", ".scr", ".ps1", ".vbs", ".js", ".jar", ".appref-ms",
    };

    private readonly MoveAssessmentOptions _options = options ?? new MoveAssessmentOptions();

    public MoveAssessment Assess(string path, CancellationToken cancellation = default)
    {
        // Check the name as given: Path.GetFullPath silently strips trailing dots and spaces, which would hide a name Win32 cannot address.
        ArgumentNullException.ThrowIfNull(path);
        var rawName = path.TrimEnd('\\', '/');
        if (rawName.EndsWith('.') || rawName.EndsWith(' '))
        {
            return Reject(MoveReason.InvalidName, false, "The name of this item ends with a dot or a space and cannot be handled safely.");
        }

        string full;
        try
        {
            full = PathUtil.Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Reject(MoveReason.InvalidName, false, "This path is not valid.");
        }

        if (PathUtil.IsSameOrUnder(full, managedRoot))
        {
            return Reject(MoveReason.InsideManagedStorage, false, "This item is already inside NextMind's managed storage.");
        }

        var name = Path.GetFileName(full);
        if (string.IsNullOrEmpty(name) || name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return Reject(MoveReason.InvalidName, false, "The name of this item cannot be handled safely.");
        }

        FileAttributes attributes;
        try
        {
            if (!fs.PathExists(full))
            {
                return Reject(MoveReason.NotFound, false, "The item no longer exists.");
            }

            attributes = fs.GetAttributes(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Reject(MoveReason.NotFound, false, "The item could not be read: " + ex.Message);
        }

        var isDirectory = attributes.HasFlag(FileAttributes.Directory);

        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return Reject(MoveReason.ReparsePoint, isDirectory, "This is a link (symlink, junction or cloud placeholder). Moving links is not supported.");
        }

        if ((attributes & OfflineFlags) != 0)
        {
            return Reject(MoveReason.OfflineOrCloudFile, isDirectory, "This item is a cloud placeholder that is not stored on this computer.");
        }

        if ((attributes & (FileAttributes.System | FileAttributes.Hidden)) != 0)
        {
            return Reject(MoveReason.SystemOrHidden, isDirectory, "System and hidden items are not moved.");
        }

        return isDirectory ? AssessDirectory(full, cancellation) : AssessFile(full);
    }

    private MoveAssessment AssessFile(string full)
    {
        long length;
        try
        {
            length = fs.GetFileLength(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Reject(MoveReason.NotFound, false, "The file could not be read: " + ex.Message);
        }

        var ext = Path.GetExtension(full);
        var reasons = new List<MoveReason>();

        if (ProgramExtensions.Contains(ext))
        {
            reasons.Add(MoveReason.Executable);
        }

        if (length > _options.SmallFileBytes && !ShortcutExtensions.Contains(ext))
        {
            reasons.Add(MoveReason.LargeFile);
        }

        if (reasons.Count == 0)
        {
            return new MoveAssessment(MoveVerdict.Allow, [], false, length, 1, false, "Safe to move.");
        }

        var message = reasons.Contains(MoveReason.LargeFile)
            ? $"This file is about {FormatSize(length)}."
            : "This is a program file. It may rely on files stored next to it.";
        return new MoveAssessment(MoveVerdict.Confirm, reasons, false, length, 1, false, message);
    }

    private MoveAssessment AssessDirectory(string full, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        long bytes = 0;
        var entries = 0;
        var truncated = false;
        var markerSeen = false;
        var pending = new Stack<string>();
        pending.Push(full);

        while (pending.Count > 0 && !truncated)
        {
            var directory = pending.Pop();
            IEnumerable<string> children;
            try
            {
                children = fs.EnumerateEntries(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Reject(MoveReason.NotFound, true, "The folder could not be read: " + ex.Message);
            }

            foreach (var child in children)
            {
                if (cancellation.IsCancellationRequested || entries >= _options.MaxEntriesScanned || clock.Elapsed > _options.Budget)
                {
                    truncated = true;
                    break;
                }

                entries++;
                FileAttributes childAttributes;
                try
                {
                    childAttributes = fs.GetAttributes(child);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue; // vanished or unreadable entry: it does not change the decision
                }

                if (childAttributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return Reject(MoveReason.ContainsReparsePoint, true, $"The folder contains a link (symlink, junction or cloud placeholder): '{Path.GetFileName(child)}'. Moving it could break the link.");
                }

                if ((childAttributes & OfflineFlags) != 0)
                {
                    return Reject(MoveReason.OfflineOrCloudFile, true, $"The folder contains a cloud placeholder that is not stored on this computer: '{Path.GetFileName(child)}'.");
                }

                if (childAttributes.HasFlag(FileAttributes.Directory))
                {
                    if (_options.ProjectMarkers.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase))
                    {
                        markerSeen = true;
                    }

                    pending.Push(child);
                }
                else
                {
                    try
                    {
                        bytes += fs.GetFileLength(child);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        // ignore: size is approximate by design
                    }
                }
            }
        }

        var reasons = new List<MoveReason>();
        if (markerSeen)
        {
            reasons.Add(MoveReason.ProjectFolder);
        }

        if (bytes >= _options.LargeFolderBytes)
        {
            reasons.Add(MoveReason.LargeFolder);
        }

        if (entries > _options.ConfirmEntryCount)
        {
            reasons.Add(MoveReason.TooManyEntries);
        }

        if (truncated)
        {
            reasons.Add(MoveReason.SizeUnknown);
        }

        if (reasons.Count == 0)
        {
            return new MoveAssessment(MoveVerdict.Allow, [], true, bytes, entries, false, "Safe to move.");
        }

        var size = truncated ? $"at least {FormatSize(bytes)} (not fully scanned)" : $"about {FormatSize(bytes)}";
        var details = new List<string> { $"This folder is {size} and has {(truncated ? "more than " : string.Empty)}{entries} item(s)." };
        if (markerSeen)
        {
            details.Add("It looks like a project (it contains .git, node_modules, bin/obj or similar).");
        }

        return new MoveAssessment(MoveVerdict.Confirm, reasons, true, bytes, entries, truncated, string.Join(' ', details));
    }

    public static string FormatSize(long bytes)
    {
        const double kb = 1024, mb = kb * 1024, gb = mb * 1024;
        var c = System.Globalization.CultureInfo.InvariantCulture;
        return bytes >= gb ? string.Create(c, $"{bytes / gb:0.0} GB") : bytes >= mb ? string.Create(c, $"{bytes / mb:0} MB") : bytes >= kb ? string.Create(c, $"{bytes / kb:0} KB") : $"{bytes} B";
    }

    private static MoveAssessment Reject(MoveReason reason, bool isDirectory, string message)
        => new(MoveVerdict.Reject, [reason], isDirectory, 0, 0, false, message);
}
