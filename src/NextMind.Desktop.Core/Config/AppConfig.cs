namespace NextMind.Desktop.Core.Config;

public static class ConfigSchema
{
    /// <summary>Version written by this build. Bump together with a new migration step in <see cref="ConfigMigrator"/>.</summary>
    /// <remarks>
    /// v1 = zones with geometry. v2 = items (references), Hidden, AutostartDecided.
    /// v3 = item Kind (Reference/Managed), AddedAtUtc, IsFolder, StoredName; zone SortMode; ManagedDesktopMovesEnabled gate.
    /// v4 = workspace profiles and assisted-layout preferences.
    /// v5 = global Future Workspace appearance preferences.
    /// </remarks>
    public const int Current = 5;
}

public static class ZoneLimits
{
    public const int MinWidth = 160;
    public const int MinHeight = 80;
    public const int MaxSize = 20000;
    public const int MaxCoordinate = 100_000;
    public const int MaxTitleLength = 80;
    public const int DefaultDpi = 96;
    public const int MaxItemsPerZone = 2000;
    public const int MaxItemNameLength = 260;
    public const string DefaultTitle = "Zone";
}

/// <summary>Reference = a path stored in the config, target untouched. Managed = a Desktop item moved into managed storage.</summary>
public enum ItemKind
{
    Reference,
    Managed,
}

public enum SortMode
{
    Custom,
    NameAscending,
    NameDescending,
    AddedNewest,
    AddedOldest,
    Type,
}

public enum AppearanceAccent
{
    ElectricBlue,
    Cyan,
    Violet,
    Amber,
}

public sealed class AppearancePreferencesConfig
{
    public double GlowIntensity { get; set; } = 0.55;

    public double PanelOpacity { get; set; } = 0.90;

    public AppearanceAccent Accent { get; set; } = AppearanceAccent.ElectricBlue;

    public bool AnimationsEnabled { get; set; } = true;

    public bool ResourceSaverMode { get; set; }
}

public sealed class AppConfig
{
    public int SchemaVersion { get; set; } = ConfigSchema.Current;

    /// <summary>Master switch behind tray "Show / Hide Zones".</summary>
    public bool ZonesVisible { get; set; } = true;

    /// <summary>
    /// False until the first run has applied the default "Start with Windows = ON" (after that the first-run default is never applied again).
    /// </summary>
    public bool AutostartDecided { get; set; }

    /// <summary>
    /// The user's preference for "Start with Windows" and the source of truth: at every start the Run entry is made to match it
    /// (missing or wrong entry is repaired when true; a leftover entry is removed when false). Only the tray checkbox changes it.
    /// Defaults to true, also for configs written before this field existed.
    /// </summary>
    public bool AutostartEnabled { get; set; } = true;

    /// <summary>
    /// SAFETY GATE. While false (the default) dropping a Desktop item creates only a reference, exactly like M2:
    /// nothing is ever moved off the real Desktop. Must be switched on deliberately (see docs/THREAT-AND-DATA-SAFETY.md).
    /// </summary>
    public bool ManagedDesktopMovesEnabled { get; set; }

    public LayoutPreferencesConfig Layout { get; set; } = new();

    public AppearancePreferencesConfig Appearance { get; set; } = new();

    public string ActiveProfileId { get; set; } = string.Empty;

    public List<WorkspaceProfileConfig> Profiles { get; set; } = [];

    public List<ZoneConfig> Zones { get; set; } = [];

    public List<WidgetConfig> Widgets { get; set; } = [];
}

public sealed class LayoutPreferencesConfig
{
    public bool SnapToElements { get; set; } = true;

    public bool SnapToGrid { get; set; }

    public int GridSize { get; set; } = 16;

    public int Gap { get; set; } = 12;

    public int SnapThreshold { get; set; } = 10;
}

public sealed class WorkspaceProfileConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public string Name { get; set; } = "Default";

    public LayoutPreferencesConfig Layout { get; set; } = new();

    public List<ZoneConfig> Zones { get; set; } = [];

    public List<WidgetConfig> Widgets { get; set; } = [];
}

public static class WidgetLimits
{
    public const int MaxNoteLength = 10_000;
}

public enum WidgetKind
{
    Clock,
    QuickNote,
}

public sealed class WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public WidgetKind Kind { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; } = 260;

    public int Height { get; set; } = 180;

    public bool Collapsed { get; set; }

    public bool Hidden { get; set; }

    public int Dpi { get; set; } = ZoneLimits.DefaultDpi;

    public string NoteText { get; set; } = string.Empty;
}

/// <summary>
/// One desktop zone. Geometry is stored in physical pixels of the virtual screen together with the
/// DPI it was saved at, so restoring does not depend on WPF's device-independent units.
/// <see cref="Height"/> is always the EXPANDED height; <see cref="Collapsed"/> says whether only the title bar is shown.
/// </summary>
public sealed class ZoneConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public string Title { get; set; } = ZoneLimits.DefaultTitle;

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; } = 320;

    public int Height { get; set; } = 220;

    public bool Collapsed { get; set; }

    public int Dpi { get; set; } = ZoneLimits.DefaultDpi;

    /// <summary>Hidden by the zone's own close button. Not a deletion: the zone and its items stay in the config.</summary>
    public bool Hidden { get; set; }

    /// <summary>How the items are DISPLAYED. The order of <see cref="Items"/> is always the user's custom order.</summary>
    public SortMode SortMode { get; set; } = SortMode.Custom;

    /// <summary>Items in the user's custom order.</summary>
    public List<ItemConfig> Items { get; set; } = [];
}

/// <summary>A zone member: either a reference to a path, or a managed item whose data lives in managed storage.</summary>
public sealed class ItemConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");

    public ItemKind Kind { get; set; } = ItemKind.Reference;

    /// <summary>Normalised absolute path of the target (for managed items: the current path inside managed storage).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>Name shown in the UI (the Shell display name captured when the item was added; kept so a missing target still has a label).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the item was added to NextMind Desktop (NOT the file's creation time). Used by "Date Added" sorting.</summary>
    public DateTime? AddedAtUtc { get; set; }

    /// <summary>Known when the item was added; null for items from older configs until the first probe.</summary>
    public bool? IsFolder { get; set; }

    /// <summary>Managed items only: the file/folder name inside <c>ManagedItems\&lt;Id&gt;\</c>.</summary>
    public string? StoredName { get; set; }
}
