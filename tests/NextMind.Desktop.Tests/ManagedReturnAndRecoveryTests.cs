using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Managed;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class ManagedReturnTests
{
    private static (ManagedFixture F, ItemConfig Item) WithManaged(string name = "Game.lnk", string content = "G", ManagedFixture? existing = null)
    {
        var f = existing ?? new ManagedFixture();
        f.CreateDesktopFile(name, content);
        var r = f.Service.MoveIntoZone(f.Zone, f.OnDesktop(name), null, f.Commit);
        Assert.True(r.Success, r.Message);
        return (f, r.Item!);
    }

    [Fact]
    public void Return_PutsTheItemBackOnTheDesktop_AndRemovesItFromTheZone()
    {
        var (f, item) = WithManaged();
        using var _ = f;

        var r = f.Service.ReturnToDesktop(f.Zone, item, f.Commit);

        Assert.True(r.Success, r.Message);
        Assert.Equal(f.OnDesktop("Game.lnk"), r.FinalPath);
        Assert.Equal("G", f.Box.Fs.ReadAllText(f.OnDesktop("Game.lnk")));
        Assert.Empty(f.Zone.Items);
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.ItemDirectory(item.Id))); // empty storage folder tidied
        Assert.Empty(f.PendingJournals());
        Assert.False(r.RenamedForConflict);
    }

    [Fact]
    public void Return_NeverOverwrites_ATakenDesktopName_GetsASuffix()
    {
        var (f, item) = WithManaged("Game.lnk", "managed");
        using var _ = f;
        f.CreateDesktopFile("Game.lnk", "someone else's file");

        var r = f.Service.ReturnToDesktop(f.Zone, item, f.Commit);

        Assert.True(r.Success, r.Message);
        Assert.True(r.RenamedForConflict);
        Assert.Equal(f.OnDesktop("Game (2).lnk"), r.FinalPath);
        Assert.Equal("someone else's file", f.Box.Fs.ReadAllText(f.OnDesktop("Game.lnk")));
        Assert.Equal("managed", f.Box.Fs.ReadAllText(f.OnDesktop("Game (2).lnk")));
    }

    [Fact]
    public void Return_ManyConflicts_PicksTheFirstFreeNumber_ForFilesAndFolders()
    {
        var (f, item) = WithManaged("Game.lnk", "m");
        using var _ = f;
        f.CreateDesktopFile("Game.lnk");
        f.CreateDesktopFile("Game (2).lnk");
        f.CreateDesktopFile("Game (3).lnk");

        Assert.Equal(f.OnDesktop("Game (4).lnk"), f.Service.ReturnToDesktop(f.Zone, item, f.Commit).FinalPath);

        f.CreateDesktopFolder("Proj.v2", ("a.txt", "A"));
        var folderMove = f.Service.MoveIntoZone(f.Zone, f.OnDesktop("Proj.v2"), null, f.Commit);
        f.CreateDesktopFolder("Proj.v2", ("b.txt", "B"));
        var back = f.Service.ReturnToDesktop(f.Zone, folderMove.Item!, f.Commit);
        Assert.Equal(f.OnDesktop("Proj.v2 (2)"), back.FinalPath); // the counter goes after a folder's whole name
        Assert.Equal("A", f.Box.Fs.ReadAllText(Path.Combine(back.FinalPath!, "a.txt")));
        Assert.Equal("B", f.Box.Fs.ReadAllText(Path.Combine(f.OnDesktop("Proj.v2"), "b.txt")));
    }

    [Fact]
    public void Return_RenameFails_ItemStaysManaged_AndInTheZone()
    {
        var (f, item) = WithManaged();
        using var _ = f;
        f.Faulty.FailOn(nameof(IFileSystem.MoveEntry));

        var r = f.Service.ReturnToDesktop(f.Zone, item, f.Commit);

        Assert.False(r.Success);
        Assert.Equal(ManagedError.IoFailure, r.Error);
        Assert.Single(f.Zone.Items);
        Assert.Equal("G", f.Box.Fs.ReadAllText(item.Path));
        Assert.False(f.Box.Fs.PathExists(f.OnDesktop("Game.lnk")));
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void Return_ConfigSaveFails_ItemGoesBackToStorage_AndStaysInTheZone_AtItsPosition()
    {
        var (f, first) = WithManaged("A.lnk", "A");
        using var _ = f;
        f.CreateDesktopFile("B.lnk", "B");
        var second = f.Service.MoveIntoZone(f.Zone, f.OnDesktop("B.lnk"), null, f.Commit).Item!;
        f.CommitSucceeds = false;

        var r = f.Service.ReturnToDesktop(f.Zone, first, f.Commit);

        Assert.Equal(ManagedError.ConfigNotSaved, r.Error);
        Assert.Equal([first.Id, second.Id], f.Zone.Items.Select(i => i.Id)); // original position restored
        Assert.Equal("A", f.Box.Fs.ReadAllText(first.Path));
        Assert.False(f.Box.Fs.PathExists(f.OnDesktop("A.lnk")));
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void Return_ConfigSaveFails_AndRollbackImpossible_RecoveryRemovesTheStaleEntry()
    {
        var (f, item) = WithManaged();
        using var _ = f;
        f.CommitSucceeds = false;
        f.Faulty.FailOn(nameof(IFileSystem.MoveEntry), skip: 1);

        var r = f.Service.ReturnToDesktop(f.Zone, item, f.Commit);

        Assert.Equal(ManagedError.ConfigNotSaved, r.Error);
        Assert.Equal("G", f.Box.Fs.ReadAllText(f.OnDesktop("Game.lnk"))); // it IS on the Desktop
        Assert.Single(f.PendingJournals());

        // The config on disk still lists it as managed (the save failed). Recover reconciles.
        var stale = new AppConfig();
        var zone = ZoneCatalog.AddZone(stale, "z", new(0, 0, 1920, 1040), 96);
        zone.Items.Add(new ItemConfig { Id = item.Id, Kind = ItemKind.Managed, Path = item.Path, Name = "Game", StoredName = "Game.lnk" });
        f.CommitSucceeds = true;

        var action = Assert.Single(f.Service.Recover(stale, f.Commit));

        Assert.Equal(RecoveryOutcome.CompletedForward, action.Outcome);
        Assert.Empty(zone.Items);
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void Return_ReferenceItem_IsRefused()
    {
        using var f = new ManagedFixture();
        ZoneItems.TryAdd(f.Zone, f.Box.Combine("ref.txt"), null, out var reference);

        var r = f.Service.ReturnToDesktop(f.Zone, reference!, f.Commit);

        Assert.Equal(ManagedError.NotManagedItem, r.Error);
        Assert.Single(f.Zone.Items);
    }

    [Fact]
    public void Return_ManagedItemMissingFromStorage_ReportsIt_AndTouchesNothing()
    {
        var (f, item) = WithManaged();
        using var _ = f;
        f.Box.Fs.MoveEntry(item.Path, f.Box.Combine("somewhere-else.lnk")); // the user (or an AV) took it away

        var r = f.Service.ReturnToDesktop(f.Zone, item, f.Commit);

        Assert.Equal(ManagedError.StorageItemMissing, r.Error);
        Assert.Single(f.Zone.Items); // the entry stays: the user decides, nothing is auto-removed
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void Return_WhenTheDesktopFolderIsGone_IsRefused()
    {
        var (f, item) = WithManaged();
        using var _ = f;
        // move the whole fake Desktop away so it no longer exists
        f.Box.Fs.MoveEntry(f.Desktop, f.Box.Combine("Desktop-renamed"));

        var r = f.Service.ReturnToDesktop(f.Zone, item, f.Commit);

        Assert.Equal(ManagedError.DestinationUnavailable, r.Error);
        Assert.Equal("G", f.Box.Fs.ReadAllText(item.Path));
    }

    [Fact]
    public void RenamingTheZone_DoesNotTouchManagedData_AndReturnStillWorks()
    {
        var (f, item) = WithManaged();
        using var _ = f;
        var pathBefore = item.Path;

        Assert.True(ZoneCatalog.Rename(f.Config, f.Zone.Id, "🎮 Gry 2.0"));

        Assert.Equal(pathBefore, f.Zone.Items[0].Path);
        Assert.Equal("G", f.Box.Fs.ReadAllText(pathBefore));
        Assert.True(f.Service.ReturnToDesktop(f.Zone, f.Zone.Items[0], f.Commit).Success);
        Assert.Equal("G", f.Box.Fs.ReadAllText(f.OnDesktop("Game.lnk")));
    }

    [Fact]
    public void MovingBetweenZones_ChangesOnlyMembership_NoDataMoves()
    {
        var (f, item) = WithManaged();
        using var _ = f;
        var other = ZoneCatalog.AddZone(f.Config, "🦈 NextMind", new(0, 0, 1920, 1040), 96);
        var storedBefore = f.Box.Fs.ReadAllText(item.Path);

        var result = ZoneOrdering.MoveToZone(f.Config, f.Zone.Id, item.Id, other.Id);

        Assert.Equal(MoveToZoneResult.Moved, result);
        Assert.Empty(f.Zone.Items);
        var moved = Assert.Single(other.Items);
        Assert.Equal(item.Path, moved.Path);
        Assert.Equal(storedBefore, f.Box.Fs.ReadAllText(moved.Path));
        Assert.Single(f.Box.Fs.EnumerateEntries(f.Paths.Root)); // still exactly one storage folder
        // and the item can be returned from its NEW zone
        Assert.True(f.Service.ReturnToDesktop(other, moved, f.Commit).Success);
    }

    [Fact]
    public void MovingAReferenceBetweenZones_Works_AndMergesADuplicate()
    {
        using var f = new ManagedFixture();
        var other = ZoneCatalog.AddZone(f.Config, "🦈 NextMind", new(0, 0, 1920, 1040), 96);
        var target = f.Box.Combine("project");
        ZoneItems.TryAdd(f.Zone, target, null, out var item);

        Assert.Equal(MoveToZoneResult.Moved, ZoneOrdering.MoveToZone(f.Config, f.Zone.Id, item!.Id, other.Id));
        Assert.Single(other.Items);

        ZoneItems.TryAdd(f.Zone, target, null, out var again);
        Assert.Equal(MoveToZoneResult.MergedIntoExisting, ZoneOrdering.MoveToZone(f.Config, f.Zone.Id, again!.Id, other.Id));
        Assert.Single(other.Items);
        Assert.Empty(f.Zone.Items);

        Assert.Equal(MoveToZoneResult.NotFound, ZoneOrdering.MoveToZone(f.Config, f.Zone.Id, "nope", other.Id));
        Assert.Equal(MoveToZoneResult.NotFound, ZoneOrdering.MoveToZone(f.Config, other.Id, other.Items[0].Id, other.Id));
    }
}

public class ZoneDeletionTests
{
    [Fact]
    public void DeletingAZoneWithReferences_RemovesConfigOnly_TargetsUntouched()
    {
        using var f = new ManagedFixture();
        var target = f.Box.Combine("ref.txt");
        f.Box.Fs.WriteAllTextDurable(target, "R");
        ZoneItems.TryAdd(f.Zone, target, null, out _);

        var r = ZoneDeletion.Delete(f.Config, f.Zone.Id, f.Service, f.Commit);

        Assert.Equal(ZoneDeleteOutcome.Deleted, r.Outcome);
        Assert.Empty(f.Config.Zones);
        Assert.Equal("R", f.Box.Fs.ReadAllText(target));
    }

    [Fact]
    public void DeletingAZoneWithManagedItems_ReturnsThemToTheDesktopFirst_NothingIsOrphaned()
    {
        using var f = new ManagedFixture();
        f.CreateDesktopFile("A.lnk", "A");
        f.CreateDesktopFile("B.lnk", "B");
        var folder = f.CreateDesktopFolder("Folder", ("in.txt", "I"));
        foreach (var n in new[] { "A.lnk", "B.lnk", "Folder" })
        {
            Assert.True(f.Service.MoveIntoZone(f.Zone, f.OnDesktop(n), null, f.Commit).Success);
        }

        Assert.Equal(3, ZoneDeletion.ManagedCount(f.Zone));

        var r = ZoneDeletion.Delete(f.Config, f.Zone.Id, f.Service, f.Commit);

        Assert.Equal(ZoneDeleteOutcome.Deleted, r.Outcome);
        Assert.Empty(f.Config.Zones);
        Assert.Equal("A", f.Box.Fs.ReadAllText(f.OnDesktop("A.lnk")));
        Assert.Equal("B", f.Box.Fs.ReadAllText(f.OnDesktop("B.lnk")));
        Assert.Equal("I", f.Box.Fs.ReadAllText(Path.Combine(folder, "in.txt")));
        Assert.Empty(f.Box.Fs.EnumerateEntries(f.Paths.Root)); // nothing left behind in managed storage
    }

    [Fact]
    public void IfAnyReturnFails_TheZoneIsNotDeleted_AndNothingIsLost()
    {
        using var f = new ManagedFixture();
        f.CreateDesktopFile("A.lnk", "A");
        f.CreateDesktopFile("B.lnk", "B");
        Assert.True(f.Service.MoveIntoZone(f.Zone, f.OnDesktop("A.lnk"), null, f.Commit).Success);
        Assert.True(f.Service.MoveIntoZone(f.Zone, f.OnDesktop("B.lnk"), null, f.Commit).Success);
        f.Faulty.FailOn(nameof(IFileSystem.MoveEntry), skip: 1); // first return ok, second fails

        var r = ZoneDeletion.Delete(f.Config, f.Zone.Id, f.Service, f.Commit);

        Assert.Equal(ZoneDeleteOutcome.ReturnFailed, r.Outcome);
        Assert.Single(f.Config.Zones); // the zone survives
        var remaining = Assert.Single(f.Zone.Items);
        Assert.Equal("B", f.Box.Fs.ReadAllText(remaining.Path)); // still safely managed
        Assert.Equal("A", f.Box.Fs.ReadAllText(f.OnDesktop("A.lnk"))); // the first one did go back
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void IfTheFinalConfigSaveFails_TheZoneIsKept()
    {
        using var f = new ManagedFixture();
        f.OnCommit = () => f.CommitSucceeds = f.Config.Zones.Count > 0; // succeeds while the zone still exists, fails once it was removed
        f.CreateDesktopFile("A.lnk", "A");
        Assert.True(f.Service.MoveIntoZone(f.Zone, f.OnDesktop("A.lnk"), null, f.Commit).Success);

        var r = ZoneDeletion.Delete(f.Config, f.Zone.Id, f.Service, f.Commit);

        Assert.Equal(ZoneDeleteOutcome.ConfigNotSaved, r.Outcome);
        Assert.Single(f.Config.Zones);
        Assert.Equal("A", f.Box.Fs.ReadAllText(f.OnDesktop("A.lnk")));
    }

    [Fact]
    public void UnknownZone_IsReported()
    {
        using var f = new ManagedFixture();
        Assert.Equal(ZoneDeleteOutcome.NotFound, ZoneDeletion.Delete(f.Config, "nope", f.Service, f.Commit).Outcome);
    }
}

public class JournalRecoveryTests
{
    private static JournalEntry MoveIn(ManagedFixture f, string state, string name = "x.lnk") => new()
    {
        OpId = "op1",
        Kind = JournalKind.MoveIn,
        State = Enum.Parse<JournalState>(state),
        ItemId = "item-009",
        ZoneId = f.Zone.Id,
        SourcePath = f.OnDesktop(name),
        DestinationPath = f.Paths.StoredPath("item-009", name),
        StoredName = name,
        DisplayName = "x",
        IsFolder = false,
        AddedAtUtc = f.Now,
    };

    private static void PlaceInStorage(ManagedFixture f, string name = "x.lnk", string content = "X")
    {
        f.Box.Fs.CreateDirectory(f.Paths.ItemDirectory("item-009"));
        f.Box.Fs.WriteAllTextDurable(f.Paths.StoredPath("item-009", name), content);
    }

    [Theory]
    [InlineData("Prepared")]
    [InlineData("Moving")]
    public void MoveIn_RenameNeverHappened_ItemStillOnDesktop_JournalDiscarded(string state)
    {
        using var f = new ManagedFixture();
        f.CreateDesktopFile("x.lnk", "X");
        f.Box.Fs.CreateDirectory(f.Paths.ItemDirectory("item-009")); // the empty per-item folder was already made
        f.Journal.Write(MoveIn(f, state), f.Now);

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.DiscardedNothingMoved, action.Outcome);
        Assert.Equal("X", f.Box.Fs.ReadAllText(f.OnDesktop("x.lnk")));
        Assert.Empty(f.Zone.Items);
        Assert.Empty(f.PendingJournals());
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.ItemDirectory("item-009")));
    }

    [Theory]
    [InlineData("Moving")] // crashed right after the rename, before "Moved" was written
    [InlineData("Moved")]
    [InlineData("ConfigCommitted")]
    public void MoveIn_RenameHappened_DataInStorage_ConfigIsBroughtInLine(string state)
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Journal.Write(MoveIn(f, state), f.Now);

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.CompletedForward, action.Outcome);
        var item = Assert.Single(f.Zone.Items);
        Assert.Equal(ItemKind.Managed, item.Kind);
        Assert.Equal("item-009", item.Id);
        Assert.Equal("X", f.Box.Fs.ReadAllText(item.Path));
        Assert.Empty(f.PendingJournals());
        Assert.Equal(1, f.CommitCalls);
    }

    [Fact]
    public void MoveIn_AlreadyInTheConfig_IsNotDuplicated()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Zone.Items.Add(new ItemConfig { Id = "item-009", Kind = ItemKind.Managed, Path = f.Paths.StoredPath("item-009", "x.lnk"), Name = "x", StoredName = "x.lnk" });
        f.Journal.Write(MoveIn(f, "ConfigCommitted"), f.Now);

        Assert.Equal(RecoveryOutcome.CompletedForward, Assert.Single(f.Service.Recover(f.Config, f.Commit)).Outcome);

        Assert.Single(f.Zone.Items);
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void MoveIn_ZoneNoLongerExists_ItemIsAdoptedIntoAnotherZone_NeverLost()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        var journal = MoveIn(f, "Moved");
        journal.ZoneId = "zone-that-was-deleted";
        f.Journal.Write(journal, f.Now);

        f.Service.Recover(f.Config, f.Commit);

        Assert.Single(f.Zone.Items);
    }

    [Fact]
    public void MoveIn_NoZonesAtAll_ARecoveredZoneIsCreated()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Journal.Write(MoveIn(f, "Moved"), f.Now);
        f.Config.Zones.Clear();

        f.Service.Recover(f.Config, f.Commit);

        var zone = Assert.Single(f.Config.Zones);
        Assert.Equal("Recovered", zone.Title);
        Assert.Single(zone.Items);
    }

    [Fact]
    public void MoveIn_BothExist_DataInStorageIsAdopted_TheDesktopItemIsNotTouched()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f, content: "managed");
        f.CreateDesktopFile("x.lnk", "a new, different file");
        f.Journal.Write(MoveIn(f, "Moved"), f.Now);

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.CompletedForward, action.Outcome);
        Assert.Equal("a new, different file", f.Box.Fs.ReadAllText(f.OnDesktop("x.lnk")));
        Assert.Equal("managed", f.Box.Fs.ReadAllText(f.Zone.Items[0].Path));
    }

    [Fact]
    public void MoveIn_ItemIsInNeitherPlace_NothingIsGuessed_JournalKept()
    {
        using var f = new ManagedFixture();
        f.Journal.Write(MoveIn(f, "Moved"), f.Now);

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.NeedsAttention, action.Outcome);
        Assert.Single(f.PendingJournals()); // kept for a human
        Assert.Empty(f.Zone.Items);
    }

    [Fact]
    public void MoveIn_ConfigCannotBeSavedDuringRecovery_JournalIsKept_ForTheNextStart()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Journal.Write(MoveIn(f, "Moved"), f.Now);
        f.CommitSucceeds = false;

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.NeedsAttention, action.Outcome);
        Assert.Single(f.PendingJournals());
        Assert.Equal("X", f.Box.Fs.ReadAllText(f.Paths.StoredPath("item-009", "x.lnk")));

        f.CommitSucceeds = true;
        Assert.Equal(RecoveryOutcome.CompletedForward, Assert.Single(f.Service.Recover(f.Config, f.Commit)).Outcome); // idempotent
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void RecoveryIsIdempotent_RunningTwiceChangesNothingTheSecondTime()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Journal.Write(MoveIn(f, "Moved"), f.Now);

        f.Service.Recover(f.Config, f.Commit);
        var second = f.Service.Recover(f.Config, f.Commit);

        Assert.Empty(second);
        Assert.Single(f.Zone.Items);
    }

    [Fact]
    public void MoveOut_ItemReachedTheDesktop_ConfigEntryIsRemoved()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Box.Fs.MoveEntry(f.Paths.StoredPath("item-009", "x.lnk"), f.OnDesktop("x.lnk"));
        f.Zone.Items.Add(new ItemConfig { Id = "item-009", Kind = ItemKind.Managed, Path = f.Paths.StoredPath("item-009", "x.lnk"), Name = "x", StoredName = "x.lnk" });
        var entry = MoveIn(f, "Moved");
        entry.Kind = JournalKind.MoveOut;
        entry.SourcePath = f.Paths.StoredPath("item-009", "x.lnk");
        entry.DestinationPath = f.OnDesktop("x.lnk");
        f.Journal.Write(entry, f.Now);

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.CompletedForward, action.Outcome);
        Assert.Empty(f.Zone.Items);
        Assert.Equal("X", f.Box.Fs.ReadAllText(f.OnDesktop("x.lnk")));
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.ItemDirectory("item-009")));
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void MoveOut_NeverLeftStorage_ItemStaysManaged()
    {
        using var f = new ManagedFixture();
        PlaceInStorage(f);
        f.Zone.Items.Add(new ItemConfig { Id = "item-009", Kind = ItemKind.Managed, Path = f.Paths.StoredPath("item-009", "x.lnk"), Name = "x", StoredName = "x.lnk" });
        var entry = MoveIn(f, "Moving");
        entry.Kind = JournalKind.MoveOut;
        entry.SourcePath = f.Paths.StoredPath("item-009", "x.lnk");
        entry.DestinationPath = f.OnDesktop("x.lnk");
        f.Journal.Write(entry, f.Now);

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.DiscardedNothingMoved, action.Outcome);
        Assert.Single(f.Zone.Items);
        Assert.Equal("X", f.Box.Fs.ReadAllText(f.Paths.StoredPath("item-009", "x.lnk")));
    }

    [Fact]
    public void MoveOut_InNeitherPlace_NeedsAttention_NothingChanged()
    {
        using var f = new ManagedFixture();
        var entry = MoveIn(f, "Moved");
        entry.Kind = JournalKind.MoveOut;
        entry.SourcePath = f.Paths.StoredPath("item-009", "x.lnk");
        entry.DestinationPath = f.OnDesktop("x.lnk");
        f.Journal.Write(entry, f.Now);

        Assert.Equal(RecoveryOutcome.NeedsAttention, Assert.Single(f.Service.Recover(f.Config, f.Commit)).Outcome);
        Assert.Single(f.PendingJournals());
    }

    [Fact]
    public void UnreadableJournal_IsReported_AndNeverDeleted()
    {
        using var f = new ManagedFixture();
        f.Box.Fs.CreateDirectory(f.Paths.JournalDirectory);
        var junk = Path.Combine(f.Paths.JournalDirectory, "broken.json");
        f.Box.Fs.WriteAllTextDurable(junk, "{ not json");

        var action = Assert.Single(f.Service.Recover(f.Config, f.Commit));

        Assert.Equal(RecoveryOutcome.NeedsAttention, action.Outcome);
        Assert.Equal("{ not json", f.Box.Fs.ReadAllText(junk));
    }

    [Fact]
    public void Journal_SurvivesACorruptedLatestGeneration_ViaTheBackup()
    {
        using var f = new ManagedFixture();
        var entry = MoveIn(f, "Prepared");
        f.Journal.Write(entry, f.Now);
        f.Journal.Write(new JournalEntry { OpId = "op1", Kind = JournalKind.MoveIn, State = JournalState.Moving, ItemId = "item-009", SourcePath = entry.SourcePath, DestinationPath = entry.DestinationPath, StoredName = "x.lnk" }, f.Now);
        var file = Path.Combine(f.Paths.JournalDirectory, "op1.json");
        f.Box.Fs.WriteAllTextDurable(file, "{ trunc"); // the newest generation is damaged

        var loaded = f.Journal.LoadPending();

        Assert.Single(loaded.Entries);
        Assert.Equal(JournalState.Prepared, loaded.Entries[0].State); // the previous generation was used
        Assert.Empty(loaded.Problems);
    }
}
