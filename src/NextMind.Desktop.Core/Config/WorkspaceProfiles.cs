namespace NextMind.Desktop.Core.Config;

public static class WorkspaceProfiles
{
    public static void EnsureInitialized(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Profiles ??= [];
        config.Profiles.RemoveAll(profile => profile is null);

        if (config.Profiles.Count == 0)
        {
            var profile = new WorkspaceProfileConfig
            {
                Name = "Default",
                Layout = Clone(config.Layout),
                Zones = CloneZones(config.Zones),
                Widgets = CloneWidgets(config.Widgets),
            };
            config.Profiles.Add(profile);
            config.ActiveProfileId = profile.Id;
            return;
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in config.Profiles)
        {
            if (string.IsNullOrWhiteSpace(profile.Id) || !ids.Add(profile.Id))
            {
                profile.Id = Guid.NewGuid().ToString("D");
                ids.Add(profile.Id);
            }

            profile.Name = NormalizeName(profile.Name, "Workspace");
            profile.Layout ??= new LayoutPreferencesConfig();
            profile.Zones ??= [];
            profile.Widgets ??= [];
        }

        var active = config.Profiles.FirstOrDefault(profile => Same(profile.Id, config.ActiveProfileId));
        if (active is null)
        {
            active = config.Profiles[0];
            config.ActiveProfileId = active.Id;
            config.Zones = CloneZones(active.Zones);
            config.Widgets = CloneWidgets(active.Widgets);
            config.Layout = Clone(active.Layout);
        }
    }

    public static void SnapshotActive(AppConfig config)
    {
        EnsureInitialized(config);
        var active = config.Profiles.First(profile => Same(profile.Id, config.ActiveProfileId));
        active.Zones = CloneZones(config.Zones);
        active.Widgets = CloneWidgets(config.Widgets);
        active.Layout = Clone(config.Layout);
    }

    public static WorkspaceProfileConfig Create(AppConfig config, string name)
    {
        SnapshotActive(config);
        var profile = new WorkspaceProfileConfig
        {
            Name = UniqueName(config, NormalizeName(name, "Workspace")),
            Layout = Clone(config.Layout),
            Zones = [],
            Widgets = [],
        };
        config.Profiles.Add(profile);
        config.ActiveProfileId = profile.Id;
        config.Layout = Clone(profile.Layout);
        config.Zones = [];
        config.Widgets = [];
        return profile;
    }

    public static bool Rename(AppConfig config, string profileId, string name)
    {
        EnsureInitialized(config);
        var profile = config.Profiles.FirstOrDefault(candidate => Same(candidate.Id, profileId));
        if (profile is null || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        profile.Name = UniqueName(config, NormalizeName(name, "Workspace"), profile.Id);
        return true;
    }

    public static WorkspaceProfileConfig? Duplicate(AppConfig config, string profileId, string name)
    {
        SnapshotActive(config);
        var source = config.Profiles.FirstOrDefault(candidate => Same(candidate.Id, profileId));
        if (source is null)
        {
            return null;
        }

        var duplicate = new WorkspaceProfileConfig
        {
            Name = UniqueName(config, NormalizeName(name, source.Name + " Copy")),
            Layout = Clone(source.Layout),
            Zones = CloneZones(source.Zones, newIds: true, includeManaged: false),
            Widgets = CloneWidgets(source.Widgets, newIds: true),
        };
        config.Profiles.Add(duplicate);
        return duplicate;
    }

    public static bool Switch(AppConfig config, string profileId)
    {
        SnapshotActive(config);
        var target = config.Profiles.FirstOrDefault(profile => Same(profile.Id, profileId));
        if (target is null)
        {
            return false;
        }

        config.ActiveProfileId = target.Id;
        config.Zones = CloneZones(target.Zones);
        config.Widgets = CloneWidgets(target.Widgets);
        config.Layout = Clone(target.Layout);
        return true;
    }

    public static bool Delete(AppConfig config, string profileId)
    {
        EnsureInitialized(config);
        if (config.Profiles.Count <= 1)
        {
            return false;
        }

        var target = config.Profiles.FirstOrDefault(profile => Same(profile.Id, profileId));
        if (target is null)
        {
            return false;
        }

        if (target.Zones.SelectMany(zone => zone.Items).Any(item => item.Kind == ItemKind.Managed))
        {
            return false;
        }

        var deletingActive = Same(config.ActiveProfileId, target.Id);
        config.Profiles.Remove(target);
        if (deletingActive)
        {
            var next = config.Profiles[0];
            config.ActiveProfileId = next.Id;
            config.Zones = CloneZones(next.Zones);
            config.Widgets = CloneWidgets(next.Widgets);
            config.Layout = Clone(next.Layout);
        }

        return true;
    }

    private static List<ZoneConfig> CloneZones(IEnumerable<ZoneConfig> zones, bool newIds = false, bool includeManaged = true)
        => zones.Select(zone => new ZoneConfig
        {
            Id = newIds ? Guid.NewGuid().ToString("D") : zone.Id,
            Title = zone.Title,
            X = zone.X,
            Y = zone.Y,
            Width = zone.Width,
            Height = zone.Height,
            Collapsed = zone.Collapsed,
            Dpi = zone.Dpi,
            Hidden = zone.Hidden,
            SortMode = zone.SortMode,
            Items = zone.Items
                .Where(item => includeManaged || item.Kind != ItemKind.Managed)
                .Select(item => new ItemConfig
                {
                    Id = newIds ? Guid.NewGuid().ToString("D") : item.Id,
                    Kind = item.Kind,
                    Path = item.Path,
                    Name = item.Name,
                    AddedAtUtc = item.AddedAtUtc,
                    IsFolder = item.IsFolder,
                    StoredName = item.StoredName,
                }).ToList(),
        }).ToList();

    private static LayoutPreferencesConfig Clone(LayoutPreferencesConfig layout)
        => new()
        {
            SnapToElements = layout.SnapToElements,
            SnapToGrid = layout.SnapToGrid,
            GridSize = layout.GridSize,
            Gap = layout.Gap,
            SnapThreshold = layout.SnapThreshold,
        };

    private static List<WidgetConfig> CloneWidgets(IEnumerable<WidgetConfig> widgets, bool newIds = false)
        => widgets.Select(widget => new WidgetConfig
        {
            Id = newIds ? Guid.NewGuid().ToString("D") : widget.Id,
            Kind = widget.Kind,
            X = widget.X,
            Y = widget.Y,
            Width = widget.Width,
            Height = widget.Height,
            Collapsed = widget.Collapsed,
            Hidden = widget.Hidden,
            Dpi = widget.Dpi,
            NoteText = widget.NoteText,
        }).ToList();

    private static string NormalizeName(string? name, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        return normalized.Length <= ZoneLimits.MaxTitleLength ? normalized : normalized[..ZoneLimits.MaxTitleLength];
    }

    private static string UniqueName(AppConfig config, string requested, string? exceptId = null)
    {
        var used = config.Profiles
            .Where(profile => exceptId is null || !Same(profile.Id, exceptId))
            .Select(profile => profile.Name)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!used.Contains(requested))
        {
            return requested;
        }

        for (var suffix = 2; ; suffix++)
        {
            var ending = $" ({suffix})";
            var stem = requested[..Math.Min(requested.Length, ZoneLimits.MaxTitleLength - ending.Length)];
            var candidate = stem + ending;
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool Same(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
