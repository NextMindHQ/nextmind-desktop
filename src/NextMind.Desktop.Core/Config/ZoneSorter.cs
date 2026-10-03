namespace NextMind.Desktop.Core.Config;

/// <summary>Logical type shown to the user (and used for "Sort by Type"); deliberately coarse: Shell/extension based, no MIME engine.</summary>
public enum ItemTypeCategory
{
    Folder = 0,
    Shortcut = 1,
    Application = 2,
    Document = 3,
    Image = 4,
    Archive = 5,
    Other = 6,
}

public static class ItemTypes
{
    private static readonly HashSet<string> Shortcuts = new(StringComparer.OrdinalIgnoreCase) { ".lnk", ".url", ".website" };

    private static readonly HashSet<string> Applications = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".com", ".scr", ".ps1", ".appref-ms", ".msix", ".appx",
    };

    private static readonly HashSet<string> Documents = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".rtf", ".pdf", ".doc", ".docx", ".odt", ".xls", ".xlsx", ".ods", ".csv", ".ppt", ".pptx", ".odp", ".json", ".xml", ".html", ".htm", ".log",
    };

    private static readonly HashSet<string> Images = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico", ".tif", ".tiff", ".heic", ".psd",
    };

    private static readonly HashSet<string> Archives = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso",
    };

    public static ItemTypeCategory Classify(string path, bool isFolder)
    {
        if (isFolder)
        {
            return ItemTypeCategory.Folder;
        }

        var ext = Path.GetExtension(path);
        if (Shortcuts.Contains(ext))
        {
            return ItemTypeCategory.Shortcut;
        }

        if (Applications.Contains(ext))
        {
            return ItemTypeCategory.Application;
        }

        if (Documents.Contains(ext))
        {
            return ItemTypeCategory.Document;
        }

        if (Images.Contains(ext))
        {
            return ItemTypeCategory.Image;
        }

        return Archives.Contains(ext) ? ItemTypeCategory.Archive : ItemTypeCategory.Other;
    }

    /// <summary>Items from older configs have no folder flag yet: an extension-less name is treated as a folder until it is probed.</summary>
    public static ItemTypeCategory Classify(ItemConfig item)
        => Classify(item.Path, item.IsFolder ?? string.IsNullOrEmpty(Path.GetExtension(item.Path)));
}

/// <summary>Display order of a zone's items. The stored list order is never changed by sorting.</summary>
public static class ZoneSorter
{
    public static IReadOnlyList<ItemConfig> Order(ZoneConfig zone) => Order(zone.Items, zone.SortMode);

    public static IReadOnlyList<ItemConfig> Order(IReadOnlyList<ItemConfig> items, SortMode mode)
    {
        // Every ordering is stable: ties keep the user's custom order.
        var indexed = items.Select((item, index) => (item, index));

        IEnumerable<(ItemConfig item, int index)> ordered = mode switch
        {
            SortMode.NameAscending => indexed.OrderBy(x => x.item.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(x => x.index),
            SortMode.NameDescending => indexed.OrderByDescending(x => x.item.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(x => x.index),
            SortMode.AddedNewest => indexed.OrderByDescending(x => x.item.AddedAtUtc).ThenByDescending(x => x.index),
            SortMode.AddedOldest => indexed.OrderBy(x => x.item.AddedAtUtc).ThenBy(x => x.index),
            SortMode.Type => indexed.OrderBy(x => ItemTypes.Classify(x.item)).ThenBy(x => x.item.Name, StringComparer.InvariantCultureIgnoreCase).ThenBy(x => x.index),
            _ => indexed,
        };

        return ordered.Select(x => x.item).ToList();
    }
}

public enum MoveToZoneResult
{
    Moved,

    /// <summary>The target zone already referenced the same path: the source entry was merged into it (nothing else changed).</summary>
    MergedIntoExisting,

    NotFound,
}

/// <summary>Manual ordering and membership changes. Pure configuration edits: no filesystem access.</summary>
public static class ZoneOrdering
{
    /// <summary>
    /// Moves an item to <paramref name="newDisplayIndex"/> of the CURRENTLY DISPLAYED order and makes that order the custom order.
    /// A manual drag in any sort mode therefore switches the zone to Custom (the arrangement the user just made is what they see).
    /// </summary>
    public static bool MoveItem(ZoneConfig zone, string itemId, int newDisplayIndex)
    {
        var displayed = ZoneSorter.Order(zone).ToList();
        var from = displayed.FindIndex(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
        if (from < 0)
        {
            return false;
        }

        var item = displayed[from];
        displayed.RemoveAt(from);
        var to = Math.Clamp(newDisplayIndex, 0, displayed.Count);
        displayed.Insert(to, item);

        var changed = zone.SortMode != SortMode.Custom || displayed.Select(i => i.Id).SequenceEqual(zone.Items.Select(i => i.Id)) is false;
        zone.Items = displayed;
        zone.SortMode = SortMode.Custom;
        return changed;
    }

    /// <summary>Changes only how items are displayed. The custom order is preserved for when the user switches back.</summary>
    public static void SetSortMode(ZoneConfig zone, SortMode mode) => zone.SortMode = Enum.IsDefined(mode) ? mode : SortMode.Custom;

    /// <summary>
    /// Moves membership from one zone to another (appended at the end of the target's custom order). Works for references and for
    /// managed items alike: no data is copied or moved — only the owning zone changes.
    /// </summary>
    public static MoveToZoneResult MoveToZone(AppConfig config, string sourceZoneId, string itemId, string targetZoneId)
    {
        var source = ZoneCatalog.Find(config, sourceZoneId);
        var target = ZoneCatalog.Find(config, targetZoneId);
        if (source is null || target is null || ReferenceEquals(source, target))
        {
            return MoveToZoneResult.NotFound;
        }

        var item = ZoneItems.Detach(source, itemId);
        if (item is null)
        {
            return MoveToZoneResult.NotFound;
        }

        if (target.Items.Any(i => string.Equals(i.Path, item.Path, StringComparison.OrdinalIgnoreCase)))
        {
            return MoveToZoneResult.MergedIntoExisting;
        }

        target.Items.Add(item);
        return MoveToZoneResult.Moved;
    }
}
