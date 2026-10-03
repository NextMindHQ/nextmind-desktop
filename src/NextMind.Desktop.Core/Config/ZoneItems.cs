using NextMind.Desktop.Core.IO;

namespace NextMind.Desktop.Core.Config;

public enum AddItemResult
{
    Added,

    /// <summary>The same target is already in this zone (nothing changed).</summary>
    Duplicate,

    /// <summary>Not a usable absolute path (relative, empty, malformed, zone full).</summary>
    Invalid,
}

/// <summary>
/// Reference-only item operations. These functions edit configuration objects and nothing else:
/// they never open, move, copy or delete the targets.
/// </summary>
public static class ZoneItems
{
    /// <summary>Absolute, normalised path, or null if <paramref name="path"/> cannot be used as a reference.</summary>
    public static string? TryNormalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        // Look at the part after an optional \\?\ long-path prefix.
        var body = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
        if (body.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || body.IndexOfAny(['?', '*']) >= 0)
        {
            return null; // includes | < > " control chars, and wildcards that GetFullPath would let through
        }

        if (body.Length > 2 && body.IndexOf(':', 2) >= 0)
        {
            return null; // alternate data streams ("file.txt:stream") are not a thing worth referencing
        }

        try
        {
            return Path.IsPathFullyQualified(path) ? PathUtil.Normalize(path) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>File/folder name for display, or the path itself for a drive root.</summary>
    public static string FallbackName(string normalizedPath)
    {
        var name = Path.GetFileName(normalizedPath);
        return string.IsNullOrEmpty(name) ? normalizedPath : name;
    }

    public static bool Contains(ZoneConfig zone, string path)
    {
        var normalized = TryNormalize(path);
        return normalized is not null && zone.Items.Any(i => string.Equals(i.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static AddItemResult TryAdd(ZoneConfig zone, string? path, string? displayName, out ItemConfig? item)
        => TryAdd(zone, path, displayName, ItemKind.Reference, isFolder: null, DateTime.UtcNow, storedName: null, out item);

    /// <summary>Adds a REFERENCE with a known folder/file flag.</summary>
    public static AddItemResult TryAddReference(ZoneConfig zone, string? path, string? displayName, bool? isFolder, DateTime addedUtc, out ItemConfig? item)
        => TryAdd(zone, path, displayName, ItemKind.Reference, isFolder, addedUtc, storedName: null, out item);

    public static AddItemResult TryAdd(
        ZoneConfig zone,
        string? path,
        string? displayName,
        ItemKind kind,
        bool? isFolder,
        DateTime addedUtc,
        string? storedName,
        out ItemConfig? item)
    {
        item = null;

        var normalized = TryNormalize(path);
        if (normalized is null || zone.Items.Count >= ZoneLimits.MaxItemsPerZone)
        {
            return AddItemResult.Invalid;
        }

        var existing = zone.Items.FirstOrDefault(i => string.Equals(i.Path, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            item = existing;
            return AddItemResult.Duplicate;
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? FallbackName(normalized) : displayName.Trim();
        if (name.Length > ZoneLimits.MaxItemNameLength)
        {
            name = name[..ZoneLimits.MaxItemNameLength];
        }

        item = new ItemConfig
        {
            Path = normalized,
            Name = name,
            Kind = kind,
            IsFolder = isFolder,
            AddedAtUtc = addedUtc.Kind == DateTimeKind.Local ? addedUtc.ToUniversalTime() : DateTime.SpecifyKind(addedUtc, DateTimeKind.Utc),
            StoredName = kind == ItemKind.Managed ? storedName ?? Path.GetFileName(normalized) : null,
        };
        zone.Items.Add(item);
        return AddItemResult.Added;
    }

    /// <summary>Takes an item out of a zone's list WITHOUT touching its target (used when moving between zones).</summary>
    public static ItemConfig? Detach(ZoneConfig zone, string itemId)
    {
        var item = zone.Items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            zone.Items.Remove(item);
        }

        return item;
    }

    /// <summary>Removes the REFERENCE only. Returns false if there was no such item.</summary>
    public static bool Remove(ZoneConfig zone, string itemId)
        => zone.Items.RemoveAll(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase)) > 0;
}
