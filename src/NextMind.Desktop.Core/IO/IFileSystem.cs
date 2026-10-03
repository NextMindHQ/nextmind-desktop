namespace NextMind.Desktop.Core.IO;

/// <summary>
/// The only way Core touches the disk. Keeps every filesystem access injectable
/// (tests run against temp directories) and funnelled through one narrow surface.
/// Deliberately has no recursive/bulk delete, no overwrite-on-move and no copy.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>True for an existing file OR directory (also true for a dangling link itself).</summary>
    bool PathExists(string path);

    void CreateDirectory(string path);

    string ReadAllText(string path);

    /// <summary>Creates or overwrites <paramref name="path"/> and flushes it to the device before returning.</summary>
    void WriteAllTextDurable(string path, string contents);

    /// <summary>
    /// Atomically replaces <paramref name="destination"/> with <paramref name="source"/>,
    /// keeping the previous destination as <paramref name="backup"/>. Destination must exist.
    /// </summary>
    void ReplaceFile(string source, string destination, string backup);

    /// <summary>Moves a file. Never overwrites: throws if <paramref name="destination"/> exists. Used for the app's own files.</summary>
    void MoveFile(string source, string destination);

    /// <summary>
    /// Renames a file OR directory to a new path on the SAME volume. Never overwrites, never copies, never falls back to
    /// copy+delete: if it cannot be done as a single atomic rename it throws and nothing has changed.
    /// This is the only operation used to relocate user data.
    /// </summary>
    void MoveEntry(string source, string destination);

    /// <summary>Housekeeping for the application's own files only (temp config, journal). Never used on user data.</summary>
    void DeleteFile(string path);

    /// <summary>Removes an EMPTY directory (throws if it has content). Used only for the app's own per-item storage folders.</summary>
    void DeleteEmptyDirectory(string path);

    IEnumerable<string> EnumerateFiles(string directory, string searchPattern);

    /// <summary>Files and directories directly inside <paramref name="directory"/> (not recursive, links are not followed).</summary>
    IEnumerable<string> EnumerateEntries(string directory);

    FileAttributes GetAttributes(string path);

    long GetFileLength(string path);
}
