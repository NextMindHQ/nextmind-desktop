namespace NextMind.Desktop.Core.Config;

/// <summary>Makes any deserialised config safe to use: sane sizes, unique non-empty ids, bounded titles.</summary>
public static class ConfigValidator
{
    public static void Normalize(AppConfig config)
    {
        config.Zones ??= [];
        config.Zones.RemoveAll(z => z is null);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in config.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.Id) || !seen.Add(zone.Id))
            {
                zone.Id = Guid.NewGuid().ToString("D");
                seen.Add(zone.Id);
            }

            zone.Title = string.IsNullOrWhiteSpace(zone.Title) ? ZoneLimits.DefaultTitle : zone.Title.Trim();
            if (zone.Title.Length > ZoneLimits.MaxTitleLength)
            {
                zone.Title = zone.Title[..ZoneLimits.MaxTitleLength];
            }

            zone.Width = Math.Clamp(zone.Width <= 0 ? 320 : zone.Width, ZoneLimits.MinWidth, ZoneLimits.MaxSize);
            zone.Height = Math.Clamp(zone.Height <= 0 ? 220 : zone.Height, ZoneLimits.MinHeight, ZoneLimits.MaxSize);
            zone.X = Math.Clamp(zone.X, -ZoneLimits.MaxCoordinate, ZoneLimits.MaxCoordinate);
            zone.Y = Math.Clamp(zone.Y, -ZoneLimits.MaxCoordinate, ZoneLimits.MaxCoordinate);
            zone.Dpi = zone.Dpi <= 0 ? ZoneLimits.DefaultDpi : Math.Clamp(zone.Dpi, 48, 960);

            NormalizeItems(zone);
        }
    }

    /// <summary>Drops unusable entries and duplicate targets (case-insensitive), keeps the order, fixes ids and names.</summary>
    private static void NormalizeItems(ZoneConfig zone)
    {
        if (!Enum.IsDefined(zone.SortMode))
        {
            zone.SortMode = SortMode.Custom;
        }

        var cleaned = new List<ItemConfig>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in zone.Items ?? [])
        {
            if (item is null)
            {
                continue;
            }

            var normalized = ZoneItems.TryNormalize(item.Path);
            if (normalized is null || !paths.Add(normalized))
            {
                continue;
            }

            item.Path = normalized;

            if (string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id))
            {
                item.Id = Guid.NewGuid().ToString("D");
                ids.Add(item.Id);
            }

            var name = string.IsNullOrWhiteSpace(item.Name) ? ZoneItems.FallbackName(normalized) : item.Name.Trim();
            item.Name = name.Length > ZoneLimits.MaxItemNameLength ? name[..ZoneLimits.MaxItemNameLength] : name;

            if (!Enum.IsDefined(item.Kind))
            {
                item.Kind = ItemKind.Reference;
            }

            // Deterministic fallback for items saved before "Date Added" existed: the Unix epoch plus the item's position in
            // the zone, in seconds. Older entries therefore sort as older, in their stored order; anything added later is newer.
            item.AddedAtUtc = item.AddedAtUtc is { } added
                ? added.Kind == DateTimeKind.Local ? added.ToUniversalTime() : DateTime.SpecifyKind(added, DateTimeKind.Utc)
                : DateTime.UnixEpoch.AddSeconds(cleaned.Count);

            item.StoredName = item.Kind == ItemKind.Managed
                ? (string.IsNullOrWhiteSpace(item.StoredName) ? System.IO.Path.GetFileName(normalized) : item.StoredName)
                : null;

            cleaned.Add(item);
            if (cleaned.Count >= ZoneLimits.MaxItemsPerZone)
            {
                break;
            }
        }

        zone.Items = cleaned;
    }
}
