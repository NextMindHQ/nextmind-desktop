using NextMind.Desktop.Core.IO;

namespace NextMind.Desktop.Core.Managed;

/// <summary>
/// Where managed items live: <c>&lt;root&gt;\&lt;item-id&gt;\&lt;original name&gt;</c>. The folder is named after the item's stable id,
/// never after the zone, so renaming or deleting a zone never relocates data, and two items with the same name can never collide.
/// </summary>
public sealed class ManagedPaths
{
    public ManagedPaths(string root, string journalDirectory)
    {
        Root = PathUtil.Normalize(root);
        JournalDirectory = PathUtil.Normalize(journalDirectory);
    }

    public string Root { get; }

    public string JournalDirectory { get; }

    public static ManagedPaths ForConfigDirectory(string configDirectory)
        => new(Path.Combine(configDirectory, "ManagedItems"), Path.Combine(configDirectory, "journal"));

    public string ItemDirectory(string itemId) => Path.Combine(Root, itemId);

    public string StoredPath(string itemId, string storedName) => Path.Combine(Root, itemId, storedName);

    public bool IsInsideStorage(string path) => PathUtil.IsSameOrUnder(path, Root);
}

public static class NameCollisions
{
    /// <summary>
    /// <c>name</c>, else <c>name (2)</c>, <c>name (3)</c> … (the counter goes before the extension for files). Never returns a taken name.
    /// </summary>
    public static string UniqueName(string name, bool isDirectory, Func<string, bool> exists)
    {
        if (!exists(name))
        {
            return name;
        }

        var stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var ext = isDirectory ? string.Empty : Path.GetExtension(name);

        for (var i = 2; i < 10_000; i++)
        {
            var candidate = $"{stem} ({i}){ext}";
            if (!exists(candidate))
            {
                return candidate;
            }
        }

        return $"{stem} ({Guid.NewGuid():N}){ext}";
    }
}
