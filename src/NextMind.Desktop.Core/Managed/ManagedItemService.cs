using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Logging;

namespace NextMind.Desktop.Core.Managed;

public enum ManagedError
{
    None,
    NotUserDesktopItem,
    NotManagedItem,
    NotFound,
    DifferentVolume,
    JournalFailure,
    IoFailure,
    DestinationUnavailable,
    StorageItemMissing,

    /// <summary>The data is safe, but the configuration could not be saved. Recovery completes it on the next start.</summary>
    ConfigNotSaved,
}

public sealed record ManagedResult(bool Success, ManagedError Error, string Message, ItemConfig? Item = null, string? FinalPath = null, bool RenamedForConflict = false)
{
    public static ManagedResult Fail(ManagedError error, string message) => new(false, error, message);
}

public enum RecoveryOutcome
{
    /// <summary>Nothing had moved; the stale journal was discarded.</summary>
    DiscardedNothingMoved,

    /// <summary>The move had happened; the configuration was brought in line with the disk.</summary>
    CompletedForward,

    /// <summary>Needs a human: data location is unknown or the journal is unreadable. Nothing was changed.</summary>
    NeedsAttention,
}

public sealed record RecoveryAction(string OpId, RecoveryOutcome Outcome, string Message);

/// <summary>
/// Managed Desktop items: the ONLY component allowed to relocate user data, and only by a same-volume, no-overwrite rename.
/// Every operation is write-ahead journaled (Prepared → Moving → Moved → ConfigCommitted → complete) so that a crash, a kill, an
/// I/O error or a failed config save at any point leaves the data in exactly one known place and the operation either
/// rolled back or completed forward on the next start. It never deletes data, never overwrites, never guesses.
/// </summary>
public sealed class ManagedItemService(
    IFileSystem fs,
    ManagedPaths paths,
    IDesktopFolders desktops,
    JournalStore journal,
    ILog? log = null,
    Func<DateTime>? clock = null,
    Func<string>? newId = null)
{
    private readonly ILog _log = log ?? NullLog.Instance;
    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.UtcNow);
    private readonly Func<string> _newId = newId ?? (() => Guid.NewGuid().ToString("D"));

    public string StoredPath(ItemConfig item) => paths.StoredPath(item.Id, item.StoredName ?? Path.GetFileName(item.Path));

    // ------------------------------------------------------------------ Desktop -> managed storage

    /// <summary>
    /// Moves one loose item from the user's Desktop into managed storage and adds it to <paramref name="zone"/>.
    /// <paramref name="commit"/> must persist the configuration and return true on success.
    /// </summary>
    public ManagedResult MoveIntoZone(ZoneConfig zone, string desktopPath, string? displayName, Func<bool> commit)
    {
        string source;
        try
        {
            source = PathUtil.Normalize(desktopPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ManagedResult.Fail(ManagedError.NotFound, "The path is not valid.");
        }

        if (DesktopClassifier.Classify(desktops, source) != ItemOrigin.UserDesktopItem)
        {
            return ManagedResult.Fail(ManagedError.NotUserDesktopItem, "Only items lying directly on your Desktop can be managed; anything else is added as a reference.");
        }

        if (!fs.PathExists(source))
        {
            return ManagedResult.Fail(ManagedError.NotFound, "The item no longer exists.");
        }

        if (!SameVolume(source, paths.Root))
        {
            return ManagedResult.Fail(ManagedError.DifferentVolume, "The Desktop and NextMind's storage are on different drives. Moving would need a copy, which is not done. Add it as a reference instead.");
        }

        var isFolder = fs.GetAttributes(source).HasFlag(FileAttributes.Directory);
        var storedName = Path.GetFileName(source);
        var id = _newId();
        var itemDirectory = paths.ItemDirectory(id);
        var destination = paths.StoredPath(id, storedName);

        if (fs.PathExists(itemDirectory))
        {
            return ManagedResult.Fail(ManagedError.IoFailure, "The storage folder for this item already exists; refusing to reuse it.");
        }

        var entry = new JournalEntry
        {
            Kind = JournalKind.MoveIn,
            State = JournalState.Prepared,
            ItemId = id,
            ZoneId = zone.Id,
            SourcePath = source,
            DestinationPath = destination,
            StoredName = storedName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? storedName : displayName.Trim(),
            IsFolder = isFolder,
            AddedAtUtc = _clock(),
        };

        // 1. Intent first. If this cannot be written durably, nothing has changed and nothing will.
        try
        {
            journal.Write(entry, _clock());
        }
        catch (Exception ex) when (IsIo(ex))
        {
            return ManagedResult.Fail(ManagedError.JournalFailure, "Could not write the safety journal, so nothing was moved: " + ex.Message);
        }

        // 2. Per-item storage folder, then record that the rename is about to happen.
        try
        {
            fs.CreateDirectory(itemDirectory);
            Advance(entry, JournalState.Moving);
        }
        catch (Exception ex) when (IsIo(ex))
        {
            CleanupEmptyDirectory(itemDirectory);
            journal.Complete(entry);
            return ManagedResult.Fail(ManagedError.JournalFailure, "Could not prepare managed storage, so nothing was moved: " + ex.Message);
        }

        // 3. The only data-touching step: an atomic rename. If it fails, the source is exactly where it was.
        try
        {
            fs.MoveEntry(source, destination);
        }
        catch (Exception ex) when (IsIo(ex))
        {
            CleanupEmptyDirectory(itemDirectory);
            journal.Complete(entry);
            _log.Warn($"Managed move of '{storedName}' failed; the item was left on the Desktop. {ex.Message}");
            return ManagedResult.Fail(ManagedError.IoFailure, $"'{storedName}' could not be moved (it may be open in another program, or there is no permission). It was left on the Desktop. {ex.Message}");
        }

        TryAdvance(entry, JournalState.Moved); // not fatal: Moving + data at destination is recovered the same way

        // 4. Commit the configuration.
        var added = ZoneItems.TryAdd(zone, destination, entry.DisplayName, ItemKind.Managed, isFolder, entry.AddedAtUtc, storedName, out var item);
        if (added != AddItemResult.Added || item is null)
        {
            return RollBackIn(entry, source, destination, itemDirectory, "The item could not be added to the zone.", ManagedError.IoFailure);
        }

        item.Id = id; // storage folder name == item id (TryAdd generated a fresh one)

        bool committed;
        try
        {
            committed = commit();
        }
        catch (Exception ex) when (IsIo(ex))
        {
            _log.Error("Config commit threw after a managed move.", ex);
            committed = false;
        }

        if (!committed)
        {
            // Try to put the item back exactly where it was and forget the entry.
            zone.Items.Remove(item);
            try
            {
                fs.MoveEntry(destination, source);
                CleanupEmptyDirectory(itemDirectory);
                journal.Complete(entry);
                return ManagedResult.Fail(ManagedError.ConfigNotSaved, "The configuration could not be saved, so the item was put back on the Desktop.");
            }
            catch (Exception ex) when (IsIo(ex))
            {
                // Could not even roll back (e.g. the name was taken meanwhile). The item is SAFE in storage: keep it in the zone in memory and let
                // the journal (state Moved) complete it forward on the next start.
                zone.Items.Add(item);
                _log.Error("Rollback after failed config save was not possible; the item stays managed and the journal is kept.", ex);
                return new ManagedResult(false, ManagedError.ConfigNotSaved, "The item is safely stored, but the configuration could not be saved yet. It will be completed automatically on the next start.", item, destination);
            }
        }

        TryAdvance(entry, JournalState.ConfigCommitted);
        journal.Complete(entry);
        return new ManagedResult(true, ManagedError.None, "Moved into the zone.", item, destination);
    }

    private ManagedResult RollBackIn(JournalEntry entry, string source, string destination, string itemDirectory, string message, ManagedError error)
    {
        try
        {
            fs.MoveEntry(destination, source);
            CleanupEmptyDirectory(itemDirectory);
            journal.Complete(entry);
            return ManagedResult.Fail(error, message + " The item was put back on the Desktop.");
        }
        catch (Exception ex) when (IsIo(ex))
        {
            _log.Error("Rollback of a managed move failed; journal kept for recovery.", ex);
            return ManagedResult.Fail(ManagedError.IoFailure, message + " The item is safely stored and will be recovered on the next start.");
        }
    }

    // ------------------------------------------------------------------ managed storage -> Desktop

    /// <summary>
    /// Returns a managed item to the user's Desktop (never overwriting: a taken name gets a " (2)" suffix) and removes it from the zone.
    /// </summary>
    public ManagedResult ReturnToDesktop(ZoneConfig zone, ItemConfig item, Func<bool> commit)
    {
        if (item.Kind != ItemKind.Managed)
        {
            return ManagedResult.Fail(ManagedError.NotManagedItem, "This item is a reference; there is nothing to return.");
        }

        var stored = StoredPath(item);
        var desktop = desktops.UserDesktop;

        if (!fs.PathExists(stored))
        {
            return ManagedResult.Fail(ManagedError.StorageItemMissing, "The managed copy is missing from storage, so nothing could be returned.");
        }

        if (!fs.DirectoryExists(desktop))
        {
            return ManagedResult.Fail(ManagedError.DestinationUnavailable, "The Desktop folder is not available.");
        }

        if (!SameVolume(stored, desktop))
        {
            return ManagedResult.Fail(ManagedError.DifferentVolume, "The Desktop and NextMind's storage are on different drives; the item cannot be returned by a safe rename.");
        }

        var isFolder = item.IsFolder ?? fs.GetAttributes(stored).HasFlag(FileAttributes.Directory);
        var storedName = item.StoredName ?? Path.GetFileName(stored);
        var finalName = NameCollisions.UniqueName(storedName, isFolder, name => fs.PathExists(Path.Combine(desktop, name)));

        var entry = new JournalEntry
        {
            Kind = JournalKind.MoveOut,
            State = JournalState.Prepared,
            ItemId = item.Id,
            ZoneId = zone.Id,
            SourcePath = stored,
            DestinationPath = Path.Combine(desktop, finalName),
            StoredName = storedName,
            DisplayName = item.Name,
            IsFolder = isFolder,
            AddedAtUtc = item.AddedAtUtc ?? _clock(),
        };

        try
        {
            journal.Write(entry, _clock());
            Advance(entry, JournalState.Moving);
        }
        catch (Exception ex) when (IsIo(ex))
        {
            journal.Complete(entry);
            return ManagedResult.Fail(ManagedError.JournalFailure, "Could not write the safety journal, so nothing was moved: " + ex.Message);
        }

        // The name was free a moment ago; if something took it in between, the rename fails (it never overwrites) and we pick the next name.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                fs.MoveEntry(stored, entry.DestinationPath);
                break;
            }
            catch (Exception ex) when (IsIo(ex))
            {
                if (attempt < 20 && fs.PathExists(entry.DestinationPath))
                {
                    finalName = NameCollisions.UniqueName(storedName, isFolder, name => fs.PathExists(Path.Combine(desktop, name)));
                    entry.DestinationPath = Path.Combine(desktop, finalName);
                    TryAdvance(entry, JournalState.Moving);
                    continue;
                }

                journal.Complete(entry);
                _log.Warn($"Return to Desktop of '{item.Name}' failed; the item stays managed. {ex.Message}");
                return ManagedResult.Fail(ManagedError.IoFailure, $"'{item.Name}' could not be returned (it may be open in another program). It stays in the zone. {ex.Message}");
            }
        }

        TryAdvance(entry, JournalState.Moved);

        var index = zone.Items.IndexOf(item);
        zone.Items.Remove(item);

        bool committed;
        try
        {
            committed = commit();
        }
        catch (Exception ex) when (IsIo(ex))
        {
            _log.Error("Config commit threw after returning an item to the Desktop.", ex);
            committed = false;
        }

        if (!committed)
        {
            try
            {
                fs.MoveEntry(entry.DestinationPath, stored);
                zone.Items.Insert(Math.Clamp(index, 0, zone.Items.Count), item);
                journal.Complete(entry);
                return ManagedResult.Fail(ManagedError.ConfigNotSaved, "The configuration could not be saved, so the item was put back in the zone.");
            }
            catch (Exception ex) when (IsIo(ex))
            {
                _log.Error("Rollback after failed config save was not possible; the item stays on the Desktop and the journal is kept.", ex);
                return new ManagedResult(false, ManagedError.ConfigNotSaved, "The item is back on the Desktop; the configuration will be brought in line on the next start.", null, entry.DestinationPath, finalName != storedName);
            }
        }

        TryAdvance(entry, JournalState.ConfigCommitted);
        journal.Complete(entry);
        CleanupEmptyDirectory(paths.ItemDirectory(item.Id));
        return new ManagedResult(true, ManagedError.None, finalName == storedName ? "Returned to the Desktop." : $"Returned to the Desktop as '{finalName}' (the name was already taken).", null, entry.DestinationPath, finalName != storedName);
    }

    /// <summary>Returns every managed item of a zone. Stops at the first failure and reports it; items already returned stay returned.</summary>
    public IReadOnlyList<ManagedResult> ReturnAllToDesktop(ZoneConfig zone, Func<bool> commit)
    {
        var results = new List<ManagedResult>();
        foreach (var item in zone.Items.Where(i => i.Kind == ItemKind.Managed).ToList())
        {
            var result = ReturnToDesktop(zone, item, commit);
            results.Add(result);
            if (!result.Success)
            {
                break;
            }
        }

        return results;
    }

    // ------------------------------------------------------------------ crash recovery

    /// <summary>
    /// Looks at every unfinished journal and decides ONLY from what is actually on disk: the data is in exactly one place, or the case is
    /// reported for a human. Never deletes data. <paramref name="commit"/> persists the configuration.
    /// </summary>
    public IReadOnlyList<RecoveryAction> Recover(AppConfig config, Func<bool> commit)
    {
        var actions = new List<RecoveryAction>();
        var loaded = journal.LoadPending();

        foreach (var problem in loaded.Problems)
        {
            actions.Add(new RecoveryAction(problem.File, RecoveryOutcome.NeedsAttention, $"Unreadable journal '{problem.File}' was left untouched."));
        }

        foreach (var entry in loaded.Entries)
        {
            actions.Add(entry.Kind == JournalKind.MoveIn ? RecoverMoveIn(config, entry, commit) : RecoverMoveOut(config, entry, commit));
        }

        return actions;
    }

    private RecoveryAction RecoverMoveIn(AppConfig config, JournalEntry entry, Func<bool> commit)
    {
        var atSource = fs.PathExists(entry.SourcePath);
        var atDestination = fs.PathExists(entry.DestinationPath);
        var itemDirectory = paths.ItemDirectory(entry.ItemId);

        if (!atDestination && atSource)
        {
            // The rename never happened (crash before it, or it failed): the item is still on the Desktop.
            CleanupEmptyDirectory(itemDirectory);
            journal.Complete(entry);
            return Action(entry, RecoveryOutcome.DiscardedNothingMoved, $"An unfinished move of '{entry.DisplayName}' was discarded; the item is still on the Desktop.");
        }

        if (!atDestination)
        {
            return Action(entry, RecoveryOutcome.NeedsAttention, $"'{entry.DisplayName}' was being moved but is in neither place. Nothing was changed; the journal '{entry.OpId}' was kept.");
        }

        // The data is in managed storage. Make sure the configuration knows about it.
        var alreadyListed = config.Zones.SelectMany(z => z.Items).Any(i => string.Equals(i.Id, entry.ItemId, StringComparison.OrdinalIgnoreCase));
        if (!alreadyListed)
        {
            var zone = ZoneCatalog.Find(config, entry.ZoneId) ?? config.Zones.FirstOrDefault() ?? RecoveredZone(config);
            ZoneItems.TryAdd(zone, entry.DestinationPath, entry.DisplayName, ItemKind.Managed, entry.IsFolder, entry.AddedAtUtc, entry.StoredName, out var item);
            if (item is not null)
            {
                item.Id = entry.ItemId;
            }
        }

        if (!SafeCommit(commit))
        {
            return Action(entry, RecoveryOutcome.NeedsAttention, $"'{entry.DisplayName}' is safe in managed storage but the configuration could not be saved; the journal was kept for the next start.");
        }

        journal.Complete(entry);
        var note = atSource ? " (a different item with the same name is on the Desktop; it was not touched)" : string.Empty;
        return Action(entry, RecoveryOutcome.CompletedForward, $"Completed an interrupted move: '{entry.DisplayName}' is managed in its zone{note}.");
    }

    private RecoveryAction RecoverMoveOut(AppConfig config, JournalEntry entry, Func<bool> commit)
    {
        var inStorage = fs.PathExists(entry.SourcePath);
        var onDesktop = fs.PathExists(entry.DestinationPath);

        if (inStorage && !onDesktop)
        {
            // Never left storage: the item is still managed and still listed.
            journal.Complete(entry);
            return Action(entry, RecoveryOutcome.DiscardedNothingMoved, $"An unfinished return of '{entry.DisplayName}' was discarded; it is still managed.");
        }

        if (!onDesktop)
        {
            return Action(entry, RecoveryOutcome.NeedsAttention, $"'{entry.DisplayName}' was being returned but is in neither place. Nothing was changed; the journal '{entry.OpId}' was kept.");
        }

        if (inStorage)
        {
            // Both exist (a new item took the name after a failed attempt). Leave everything as it is.
            journal.Complete(entry);
            return Action(entry, RecoveryOutcome.NeedsAttention, $"'{entry.DisplayName}' exists both in storage and on the Desktop. Nothing was changed.");
        }

        // It is on the Desktop. Drop the managed entry from the configuration.
        foreach (var zone in config.Zones)
        {
            zone.Items.RemoveAll(i => string.Equals(i.Id, entry.ItemId, StringComparison.OrdinalIgnoreCase));
        }

        if (!SafeCommit(commit))
        {
            return Action(entry, RecoveryOutcome.NeedsAttention, $"'{entry.DisplayName}' is back on the Desktop but the configuration could not be saved; the journal was kept for the next start.");
        }

        journal.Complete(entry);
        CleanupEmptyDirectory(paths.ItemDirectory(entry.ItemId));
        return Action(entry, RecoveryOutcome.CompletedForward, $"Completed an interrupted return: '{entry.DisplayName}' is on the Desktop.");
    }

    private static ZoneConfig RecoveredZone(AppConfig config)
    {
        var zone = new ZoneConfig { Title = "Recovered", X = 40, Y = 40, Width = 360, Height = 260 };
        config.Zones.Add(zone);
        return zone;
    }

    // ------------------------------------------------------------------ helpers

    private RecoveryAction Action(JournalEntry entry, RecoveryOutcome outcome, string message)
    {
        _log.Info($"Recovery [{entry.OpId}] {outcome}: {message}");
        return new RecoveryAction(entry.OpId, outcome, message);
    }

    private bool SafeCommit(Func<bool> commit)
    {
        try
        {
            return commit();
        }
        catch (Exception ex) when (IsIo(ex))
        {
            _log.Error("Config commit failed during recovery.", ex);
            return false;
        }
    }

    private void Advance(JournalEntry entry, JournalState state)
    {
        entry.State = state;
        journal.Write(entry, _clock());
    }

    private void TryAdvance(JournalEntry entry, JournalState state)
    {
        try
        {
            Advance(entry, state);
        }
        catch (Exception ex) when (IsIo(ex))
        {
            _log.Warn($"Could not record journal state {state}: {ex.Message}");
        }
    }

    private void CleanupEmptyDirectory(string directory)
    {
        try
        {
            if (fs.DirectoryExists(directory) && !fs.EnumerateEntries(directory).Any())
            {
                fs.DeleteEmptyDirectory(directory);
            }
        }
        catch (Exception ex) when (IsIo(ex))
        {
            _log.Warn($"Could not remove the empty folder '{directory}': {ex.Message}");
        }
    }

    private static bool SameVolume(string a, string b)
        => string.Equals(Path.GetPathRoot(PathUtil.Normalize(a)), Path.GetPathRoot(PathUtil.Normalize(b)), StringComparison.OrdinalIgnoreCase);

    private static bool IsIo(Exception ex) => ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException;
}
