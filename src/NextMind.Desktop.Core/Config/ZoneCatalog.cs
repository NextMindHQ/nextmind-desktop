using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Core.Config;

public static class ZoneNames
{
    /// <summary>Trimmed, length-limited name, or null when nothing usable was typed.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var name = input.Trim();
        return name.Length > ZoneLimits.MaxTitleLength ? name[..ZoneLimits.MaxTitleLength].TrimEnd() : name;
    }
}

/// <summary>
/// Zone-level configuration operations. Deleting a zone removes configuration only: the items are references
/// and the targets are never touched.
/// </summary>
public static class ZoneCatalog
{
    public static ZoneConfig AddZone(AppConfig config, string? name, RectPx workArea, int dpi)
    {
        var title = ZoneNames.Normalize(name) ?? ZoneLimits.DefaultTitle;
        var zone = ZoneFactory.Create(title, workArea, dpi, config.Zones.Count);
        config.Zones.Add(zone);
        config.ZonesVisible = true;
        return zone;
    }

    public static bool Rename(AppConfig config, string zoneId, string? newName)
    {
        var name = ZoneNames.Normalize(newName);
        var zone = Find(config, zoneId);
        if (name is null || zone is null)
        {
            return false;
        }

        zone.Title = name;
        return true;
    }

    /// <summary>Removes the zone and its references from the configuration. Targets are not touched.</summary>
    public static bool Delete(AppConfig config, string zoneId)
        => config.Zones.RemoveAll(z => string.Equals(z.Id, zoneId, StringComparison.OrdinalIgnoreCase)) > 0;

    public static bool SetHidden(AppConfig config, string zoneId, bool hidden)
    {
        var zone = Find(config, zoneId);
        if (zone is null)
        {
            return false;
        }

        zone.Hidden = hidden;
        return true;
    }

    public static bool IsVisible(AppConfig config, ZoneConfig zone) => config.ZonesVisible && !zone.Hidden;

    /// <summary>
    /// Tray "Show / Hide Zones": if anything is visible, hide everything (zones keep their own Hidden flags);
    /// otherwise show everything, including zones hidden individually with their ×. Returns true when zones are now shown.
    /// </summary>
    public static bool ToggleAll(AppConfig config)
    {
        var anyVisible = config.ZonesVisible && config.Zones.Any(z => !z.Hidden);
        if (anyVisible)
        {
            config.ZonesVisible = false;
            return false;
        }

        ShowAll(config);
        return true;
    }

    public static void ShowAll(AppConfig config)
    {
        config.ZonesVisible = true;
        foreach (var zone in config.Zones)
        {
            zone.Hidden = false;
        }
    }

    public static ZoneConfig? Find(AppConfig config, string zoneId)
        => config.Zones.FirstOrDefault(z => string.Equals(z.Id, zoneId, StringComparison.OrdinalIgnoreCase));
}
