using System.IO;
using System.Windows.Threading;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Core.Managed;
using NextMind.Desktop.Core.Startup;
using NextMind.Desktop.Shell;

namespace NextMind.Desktop.App;

/// <summary>
/// Owns the zone windows, persistence and the decisions around items.
/// Rules: a dropped item that lies directly on the user's Desktop may become a MANAGED item (moved into managed storage) — but only when the
/// safety gate is on, only after the risk assessment, and only through <see cref="ManagedItemService"/>; everything else becomes a REFERENCE.
/// Saving is event-driven: any change (re)arms one one-shot timer (400 ms debounce); nothing ticks while idle.
/// Deleting a zone never orphans managed data (it is returned to the Desktop first).
/// </summary>
public sealed class ZoneManager
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly AppConfig _config;
    private readonly ConfigStore _store;
    private readonly DesktopLayer _layer;
    private readonly ILog _log;
    private readonly IconService _icons;
    private readonly ManagedItemService _service;
    private readonly MoveAssessor _assessor;
    private readonly IDesktopFolders _desktops;
    private readonly Func<bool> _managedMovesEnabled;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _pinTimer;
    private readonly List<ZoneWindow> _windows = [];
    private int _pinAttempt;

    public ZoneManager(
        AppConfig config,
        ConfigStore store,
        DesktopLayer layer,
        ILog log,
        Dispatcher dispatcher,
        IconService icons,
        ManagedItemService service,
        MoveAssessor assessor,
        IDesktopFolders desktops,
        Func<bool> managedMovesEnabled)
    {
        _config = config;
        _store = store;
        _layer = layer;
        _log = log;
        _icons = icons;
        _service = service;
        _assessor = assessor;
        _desktops = desktops;
        _managedMovesEnabled = managedMovesEnabled;

        _saveTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = SaveDelay };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Flush();
        };

        // One-shot, only armed while a zone could not yet find the desktop layer (e.g. very early after logon).
        _pinTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _pinTimer.Tick += (_, _) => CheckPins();
    }

    public IReadOnlyList<ZoneWindow> Windows => _windows;

    public IReadOnlyList<ZoneConfig> Zones => _config.Zones;

    public bool ManagedMovesEnabled => _managedMovesEnabled();

    /// <summary>Raised when zones were added/removed/renamed/shown/hidden (the Control Center refreshes on it).</summary>
    public event Action? Changed;

    /// <summary>Raised once, when the first zone has actually been rendered (used for the cold-start measurement).</summary>
    public event Action? FirstZoneRendered;

    public bool IsShown(ZoneConfig zone) => ZoneCatalog.IsVisible(_config, zone);

    // ------------------------------------------------------------------ opening / creating

    public void OpenConfiguredZones()
    {
        foreach (var zone in _config.Zones.ToList())
        {
            Open(zone);
        }

        EnsureAnchored();
    }

    public void CreateZone(string name)
    {
        var zone = ZoneCatalog.AddZone(_config, name, Monitors.GetPrimaryWorkArea(), Monitors.GetSystemDpi());
        ApplyVisibility();
        Open(zone);
        _log.Info($"Created zone '{zone.Title}'.");
        EnsureAnchored();
        Flush();
        Changed?.Invoke();
    }

    /// <summary>Runs crash recovery for unfinished managed operations. Call once at startup BEFORE the zones are opened.</summary>
    public IReadOnlyList<RecoveryAction> RecoverInterruptedOperations()
    {
        var actions = _service.Recover(_config, Commit);
        foreach (var a in actions)
        {
            _log.Info($"Recovery: {a.Outcome} - {a.Message}");
        }

        return actions;
    }

    // ------------------------------------------------------------------ visibility

    /// <summary>Tray "Show / Hide Zones".</summary>
    public void ToggleShowHide()
    {
        var shown = ZoneCatalog.ToggleAll(_config);
        ApplyVisibility();
        if (shown)
        {
            RepinAll();
        }

        ScheduleSave();
        Changed?.Invoke();
    }

    /// <summary>Brings every zone back (second launch, Control Center "Show All Zones").</summary>
    public void ShowAll()
    {
        ZoneCatalog.ShowAll(_config);
        ApplyVisibility();
        RepinAll();
        ScheduleSave();
        Changed?.Invoke();
    }

    public void HideAll()
    {
        _config.ZonesVisible = false;
        ApplyVisibility();
        ScheduleSave();
        Changed?.Invoke();
    }

    /// <summary>Control Center per-zone Show/Hide. Showing one zone while everything was hidden globally shows only that zone.</summary>
    public void ToggleZoneShown(string zoneId)
    {
        var zone = ZoneCatalog.Find(_config, zoneId);
        if (zone is null)
        {
            return;
        }

        if (IsShown(zone))
        {
            zone.Hidden = true;
        }
        else
        {
            if (!_config.ZonesVisible)
            {
                foreach (var other in _config.Zones.Where(z => z != zone))
                {
                    other.Hidden = true;
                }

                _config.ZonesVisible = true;
            }

            zone.Hidden = false;
        }

        ApplyVisibility();
        RepinAll();
        ScheduleSave();
        Changed?.Invoke();
    }

    public void RenameZoneById(string zoneId)
    {
        var window = _windows.FirstOrDefault(w => string.Equals(w.Config.Id, zoneId, StringComparison.OrdinalIgnoreCase));
        if (window is not null)
        {
            OnRename(window);
        }
    }

    /// <summary>Explorer restarted or the desktop host changed: forget the cached host and re-pin every zone.</summary>
    public void RepinAll()
    {
        _layer.Invalidate();
        _log.Info("Re-anchoring zones. " + _layer.Describe());
        foreach (var w in _windows.Where(w => w.IsVisible))
        {
            w.Repin();
        }

        EnsureAnchored();
    }

    public void ClampAll()
    {
        var areas = Monitors.GetWorkAreas();
        foreach (var w in _windows)
        {
            w.ClampToWorkAreas(areas);
        }

        _log.Info($"Display changed: {areas.Count} work area(s); zones clamped.");
        ScheduleSave();
    }

    // ------------------------------------------------------------------ persistence

    /// <summary>Snapshots live geometry and writes the config now (also used on exit / session end).</summary>
    public void Flush()
    {
        _saveTimer.Stop();
        if (!TrySave())
        {
            _log.Warn("The configuration could not be saved.");
        }

        Changed?.Invoke();
    }

    private void SnapshotAll()
    {
        foreach (var w in _windows)
        {
            w.SnapshotInto(w.Config);
        }
    }

    private bool TrySave()
    {
        SnapshotAll();
        try
        {
            if (!_store.Save(_config))
            {
                _log.Warn("Config is read-only; changes were not saved.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _log.Error("Saving config failed; the previous config is untouched.", ex);
            return false;
        }
    }

    /// <summary>The commit callback handed to the managed-items service: persist the config, true on success.</summary>
    private bool Commit() => TrySave();

    // ------------------------------------------------------------------ windows

    private void Open(ZoneConfig zone)
    {
        var window = new ZoneWindow(zone, _layer, _log, _icons) { ZoneProvider = () => _config.Zones };
        window.ZoneChanged += _ => ScheduleSave();
        window.RenameRequested += OnRename;
        window.HideRequested += OnHide;
        window.DeleteRequested += OnDelete;
        window.PathsDropped += OnPathsDropped;
        window.ReturnToDesktopRequested += OnReturnToDesktop;
        window.MoveToZoneRequested += OnMoveToZone;
        window.ItemDroppedFromOtherZone += OnItemDroppedFromOtherZone;

        if (_windows.Count == 0)
        {
            window.ContentRendered += OnFirstRendered;
        }

        _windows.Add(window);

        if (ZoneCatalog.IsVisible(_config, zone))
        {
            window.Show();
        }
    }

    private void ApplyVisibility()
    {
        foreach (var w in _windows)
        {
            var wanted = ZoneCatalog.IsVisible(_config, w.Config);
            if (wanted && !w.IsVisible)
            {
                w.Show();
            }
            else if (!wanted && w.IsVisible)
            {
                w.Hide();
            }
        }
    }

    private ZoneWindow? WindowFor(string zoneId)
        => _windows.FirstOrDefault(w => string.Equals(w.Config.Id, zoneId, StringComparison.OrdinalIgnoreCase));

    private void OnRename(ZoneWindow window)
    {
        var name = PromptWindow.AskText("Rename Zone", "Zone name:", window.Config.Title, "Rename");
        if (name is not null && ZoneCatalog.Rename(_config, window.Config.Id, name))
        {
            // Managed data lives under item ids, not zone names: nothing on disk changes.
            window.SetTitle(window.Config.Title);
            _log.Info($"Renamed zone to '{window.Config.Title}'.");
            Flush();
        }
    }

    /// <summary>The zone's × and "Hide Zone": hides only. The zone and its items stay in the config.</summary>
    private void OnHide(ZoneWindow window)
    {
        if (ZoneCatalog.SetHidden(_config, window.Config.Id, true))
        {
            window.Snapshot();
            window.Hide();
            _log.Info($"Hid zone '{window.Config.Title}'.");
            Flush();
        }
    }

    private void OnDelete(ZoneWindow window)
    {
        var zone = window.Config;
        var managed = ZoneDeletion.ManagedCount(zone);

        if (managed > 0)
        {
            // The zone owns data inside managed storage: it must never be orphaned, so the only way forward is to give it back first.
            var choice = ChoiceWindow.Ask(
                "Delete Zone",
                $"Zone contains {managed} managed desktop item{(managed == 1 ? string.Empty : "s")}.\n\nBefore deleting this Zone choose:",
                ["Return items to Desktop and delete Zone", "Cancel"],
                defaultIndex: 1,
                cancelIndex: 1);

            if (choice != 0)
            {
                return;
            }
        }
        else if (!PromptWindow.Confirm("Delete Zone", $"Delete zone \"{zone.Title}\"?\nFiles and folders will NOT be deleted.", "Delete Zone"))
        {
            return;
        }

        var result = ZoneDeletion.Delete(_config, zone.Id, _service, Commit);
        switch (result.Outcome)
        {
            case ZoneDeleteOutcome.Deleted:
                _windows.Remove(window);
                window.Close();
                _log.Info($"Deleted zone '{zone.Title}' (configuration only; {result.Returned.Count} managed item(s) returned to the Desktop).");
                Flush();
                break;

            default:
                window.ItemsChangedExternally();
                _log.Warn($"Zone '{zone.Title}' was not deleted: {result.Message}");
                ChoiceWindow.Inform("Zone not deleted", result.Message);
                Changed?.Invoke();
                break;
        }
    }

    // ------------------------------------------------------------------ dropped files: reference or managed move

    private async void OnPathsDropped(ZoneWindow window, IReadOnlyList<string> paths)
    {
        try
        {
            foreach (var path in paths)
            {
                await HandleDroppedAsync(window, path);
            }
        }
        catch (Exception ex)
        {
            _log.Error("Handling dropped items failed.", ex);
        }
    }

    private ItemOrigin Classify(string path)
    {
        try
        {
            return DesktopClassifier.Classify(_desktops, path);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return ItemOrigin.External;
        }
    }

    private async Task HandleDroppedAsync(ZoneWindow window, string path)
    {
        var origin = Classify(path);
        _log.Info($"Dropped '{Path.GetFileName(path)}': source classified as {origin}; managed moves {(_managedMovesEnabled() ? "enabled" : "disabled")}.");

        if (origin != ItemOrigin.UserDesktopItem)
        {
            // Outside the user's Desktop (and the shared Public Desktop): never moved, only referenced.
            window.AddReference(path);
            return;
        }

        if (!_managedMovesEnabled())
        {
            _log.Info("Managed Desktop Moves are disabled: a Desktop item was added as a reference only; it stays on the Desktop.");
            window.AddReference(path);
            return;
        }

        // Risk assessment: read-only, bounded, off the UI thread (a big folder must never freeze the zones).
        var assessment = await Task.Run(() => _assessor.Assess(path));
        if (!_windows.Contains(window))
        {
            return;
        }

        var name = Path.GetFileName(path);
        switch (assessment.Verdict)
        {
            case MoveVerdict.Allow:
                MoveIntoZone(window, path);
                break;

            case MoveVerdict.Confirm:
            {
                var choice = ChoiceWindow.Ask(
                    "Move into Zone?",
                    $"\"{name}\" is on your Desktop.\n\n{assessment.Message}\n\nMoving it takes it off the Desktop and into the zone. Adding a reference leaves it exactly where it is.",
                    ["Move into Zone", "Add as Reference", "Cancel"],
                    defaultIndex: 1,
                    cancelIndex: 2);

                if (choice == 0)
                {
                    MoveIntoZone(window, path);
                }
                else if (choice == 1)
                {
                    window.AddReference(path);
                }

                break;
            }

            default:
            {
                var choice = ChoiceWindow.Ask(
                    "This item can't be moved",
                    $"\"{name}\" can't be moved into the zone.\n\n{assessment.Message}\n\nYou can add it as a reference instead; it stays on your Desktop.",
                    ["Add as Reference", "Cancel"],
                    defaultIndex: 0,
                    cancelIndex: 1);

                if (choice == 0)
                {
                    window.AddReference(path);
                }

                break;
            }
        }
    }

    private void MoveIntoZone(ZoneWindow window, string path)
    {
        string? displayName = null;
        try
        {
            displayName = ShellIcons.TryGetDisplayName(path);
        }
        catch (Exception ex)
        {
            _log.Warn($"Display name lookup failed: {ex.Message}");
        }

        var result = _service.MoveIntoZone(window.Config, path, displayName, Commit);
        if (result.Success || result.Item is not null)
        {
            window.ItemsChangedExternally();
        }

        if (!result.Success)
        {
            _log.Warn($"Managed move failed: {result.Error} - {result.Message}");
            ChoiceWindow.Inform("Could not move the item", result.Message);
        }
        else
        {
            _log.Info($"Moved '{result.Item?.Name}' from the Desktop into zone '{window.Config.Title}' (managed).");
            Changed?.Invoke();
        }
    }

    private void OnReturnToDesktop(ZoneWindow window, ItemConfig item)
    {
        var result = _service.ReturnToDesktop(window.Config, item, Commit);
        window.ItemsChangedExternally();

        if (!result.Success)
        {
            _log.Warn($"Return to Desktop failed: {result.Error} - {result.Message}");
            ChoiceWindow.Inform("Could not return the item", result.Message);
        }
        else if (result.RenamedForConflict)
        {
            ChoiceWindow.Inform("Returned to the Desktop", result.Message);
        }

        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ membership changes (configuration only)

    private void OnMoveToZone(ZoneWindow source, ItemConfig item, string targetZoneId)
    {
        MoveBetweenZones(source.Config.Id, item.Id, targetZoneId, insertIndex: null);
    }

    private void OnItemDroppedFromOtherZone(ZoneWindow target, ZoneItemDragPayload payload, int insertIndex)
    {
        MoveBetweenZones(payload.ZoneId, payload.ItemId, target.Config.Id, insertIndex);
    }

    private void MoveBetweenZones(string sourceZoneId, string itemId, string targetZoneId, int? insertIndex)
    {
        var result = ZoneOrdering.MoveToZone(_config, sourceZoneId, itemId, targetZoneId);
        if (result == MoveToZoneResult.NotFound)
        {
            return;
        }

        var target = ZoneCatalog.Find(_config, targetZoneId);
        if (result == MoveToZoneResult.Moved && insertIndex is { } index && target is not null)
        {
            ZoneOrdering.MoveItem(target, itemId, index); // user dropped it at a specific place: keep it there (switches the target to Custom)
        }

        WindowFor(sourceZoneId)?.ItemsChangedExternally();
        WindowFor(targetZoneId)?.ItemsChangedExternally();
        Flush();
    }

    // ------------------------------------------------------------------ plumbing

    private void OnFirstRendered(object? sender, EventArgs e)
    {
        if (sender is ZoneWindow w)
        {
            w.ContentRendered -= OnFirstRendered;
        }

        FirstZoneRendered?.Invoke();
    }

    /// <summary>Starts (or restarts) the bounded retry for zones that could not reach the desktop layer yet.</summary>
    public void EnsureAnchored()
    {
        _pinAttempt = 0;
        CheckPins();
    }

    private void CheckPins()
    {
        _pinTimer.Stop();

        var allPinned = true;
        foreach (var w in _windows.Where(w => w.IsVisible))
        {
            if (!w.IsPinned && !w.Repin())
            {
                allPinned = false;
            }
        }

        if (allPinned)
        {
            return;
        }

        var delay = BackoffSchedule.Next(_pinAttempt++);
        if (delay is null)
        {
            _log.Warn("Desktop layer still not found after the retry window; zones stay in the visible fallback z-order until Explorer announces itself (TaskbarCreated).");
            return;
        }

        _pinTimer.Interval = delay.Value;
        _pinTimer.Start();
    }

    private void ScheduleSave()
    {
        // Restarting a stopped/running one-shot timer: cheap, and no ticking while idle.
        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
