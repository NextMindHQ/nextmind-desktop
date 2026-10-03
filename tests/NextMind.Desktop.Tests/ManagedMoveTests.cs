using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Managed;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class DesktopDetectionTests
{
    private static readonly StaticDesktopFolders Desktops = new(@"C:\Users\someone\Desktop", @"C:\Users\Public\Desktop");

    [Theory]
    [InlineData(@"C:\Users\someone\Desktop\Battlefield 6.lnk", ItemOrigin.UserDesktopItem)]
    [InlineData(@"c:\users\SOMEONE\desktop\Battlefield 6.lnk", ItemOrigin.UserDesktopItem)]
    [InlineData(@"C:\Users\someone\Desktop\Folder\", ItemOrigin.UserDesktopItem)]
    [InlineData(@"C:\Users\someone\Desktop\.\x\..\Projekt Źródło", ItemOrigin.UserDesktopItem)]
    [InlineData(@"C:\Users\Public\Desktop\Shared.lnk", ItemOrigin.PublicDesktopItem)]
    [InlineData(@"C:\NextMind\nextmind-business-ai", ItemOrigin.External)]
    [InlineData(@"C:\Users\someone\Desktop\Notatki\inner.txt", ItemOrigin.External)] // nested in a Desktop subfolder: not a loose item
    [InlineData(@"C:\Users\someone\Desktop", ItemOrigin.External)] // the Desktop folder itself
    [InlineData(@"C:\Users\someone\Desktop2\x.lnk", ItemOrigin.External)] // sibling with a shared prefix
    [InlineData(@"C:\Users\someone\Documents\x.lnk", ItemOrigin.External)]
    [InlineData(@"D:\Users\someone\Desktop\x.lnk", ItemOrigin.External)]
    [InlineData(@"relative\x.lnk", ItemOrigin.External)]
    public void Classify(string path, ItemOrigin expected) => Assert.Equal(expected, DesktopClassifier.Classify(Desktops, path));

    [Fact]
    public void RedirectedDesktop_IsHonoured_NotAHardCodedPath()
    {
        var redirected = new StaticDesktopFolders(@"D:\Moje\Pulpit", null);

        Assert.Equal(ItemOrigin.UserDesktopItem, DesktopClassifier.Classify(redirected, @"D:\Moje\Pulpit\gra.lnk"));
        Assert.Equal(ItemOrigin.External, DesktopClassifier.Classify(redirected, @"C:\Users\someone\Desktop\gra.lnk"));
    }

    [Fact]
    public void NoPublicDesktop_NothingIsPublic()
        => Assert.Equal(ItemOrigin.External, DesktopClassifier.Classify(new StaticDesktopFolders(@"C:\U\Desktop", null), @"C:\Users\Public\Desktop\x.lnk"));
}

public class RealDesktopSafetyGuardTests
{
    [Fact]
    public void Guard_FailsFast_OnTheRealDesktop_PublicDesktop_Documents_Pictures()
    {
        foreach (var real in ProtectedPaths.FromEnvironment())
        {
            Assert.Throws<InvalidOperationException>(() => RealDesktopGuard.AssertFake(real));
            Assert.Throws<InvalidOperationException>(() => RealDesktopGuard.AssertFake(Path.Combine(real, "fake-desktop")));
        }
    }

    [Fact]
    public void Guard_FailsFast_OutsideTemp()
        => Assert.Throws<InvalidOperationException>(() => RealDesktopGuard.AssertFake(Path.GetPathRoot(Path.GetTempPath())! + "NotTemp"));

    [Fact]
    public void Fixture_RootsAreInsideTemp_AndNotInsideAnyRealUserFolder()
    {
        using var f = new ManagedFixture();

        Assert.All(new[] { f.Desktop, f.PublicDesktop, f.Paths.Root, f.Paths.JournalDirectory }, p =>
        {
            RealDesktopGuard.AssertFake(p);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(p), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ServicePointedAtTheRealDesktop_IsStoppedByTheGuardedFilesystem_BeforeAnyChange()
    {
        using var box = new TempSandbox();
        var realDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var paths = ManagedPaths.ForConfigDirectory(box.Combine("Config"));
        var journal = new JournalStore(box.Fs, paths.JournalDirectory);
        var service = new ManagedItemService(box.Fs, paths, new StaticDesktopFolders(realDesktop), journal);
        var zone = new ZoneConfig();

        Assert.Throws<ProtectedPathException>(() => service.MoveIntoZone(zone, Path.Combine(realDesktop, "anything.lnk"), null, () => true));

        Assert.Empty(zone.Items);
        Assert.False(box.Fs.DirectoryExists(paths.JournalDirectory));
        Assert.False(box.Fs.DirectoryExists(paths.Root));
    }
}

public class ManagedMoveInTests
{
    [Fact]
    public void ShortcutFromTheDesktop_BecomesAManagedItem_AndLeavesTheDesktop()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("Battlefield 6.lnk", "shortcut-bytes");

        var r = f.Service.MoveIntoZone(f.Zone, lnk, "Battlefield 6", f.Commit);

        Assert.True(r.Success, r.Message);
        Assert.False(f.Box.Fs.PathExists(lnk)); // gone from the loose Desktop
        var item = Assert.Single(f.Zone.Items);
        Assert.Equal(ItemKind.Managed, item.Kind);
        Assert.Equal("Battlefield 6.lnk", item.StoredName);
        Assert.Equal(Path.Combine(f.Paths.Root, "item-001", "Battlefield 6.lnk"), item.Path);
        Assert.Equal("shortcut-bytes", f.Box.Fs.ReadAllText(item.Path)); // the one copy, intact
        Assert.Equal(f.Now, item.AddedAtUtc);
        Assert.False(item.IsFolder);
        Assert.Empty(f.PendingJournals()); // COMPLETE removes the journal
        Assert.Equal(1, f.CommitCalls);
    }

    [Fact]
    public void FolderFromTheDesktop_MovesWithItsWholeContent()
    {
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("Projekt Źródło", ("a.txt", "A"), ("sub\\b.txt", "B"));
        f.Box.Fs.CreateDirectory(Path.Combine(dir, "sub"));
        f.Box.Fs.WriteAllTextDurable(Path.Combine(dir, "sub", "b.txt"), "B");

        var r = f.Service.MoveIntoZone(f.Zone, dir, null, f.Commit);

        Assert.True(r.Success, r.Message);
        Assert.False(f.Box.Fs.DirectoryExists(dir));
        var stored = r.Item!.Path;
        Assert.Equal("A", f.Box.Fs.ReadAllText(Path.Combine(stored, "a.txt")));
        Assert.Equal("B", f.Box.Fs.ReadAllText(Path.Combine(stored, "sub", "b.txt")));
        Assert.True(r.Item.IsFolder);
    }

    [Fact]
    public void JournalIsOnDisk_WhileTheConfigIsBeingCommitted_InStateMoved()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk");
        JournalEntry? seen = null;
        f.OnCommit = () => seen = f.Journal.LoadPending().Entries.SingleOrDefault();

        f.Service.MoveIntoZone(f.Zone, lnk, null, f.Commit);

        Assert.NotNull(seen);
        Assert.Equal(JournalKind.MoveIn, seen!.Kind);
        Assert.Equal(JournalState.Moved, seen.State);
        Assert.Equal(lnk, seen.SourcePath);
        Assert.Equal(f.Zone.Id, seen.ZoneId);
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void ItemOutsideTheUserDesktop_IsNotMoved_ReferenceIsTheCallersJob()
    {
        using var f = new ManagedFixture();
        var external = f.Box.Combine("Elsewhere", "business-ai");
        f.Box.Fs.CreateDirectory(external);
        f.Box.Fs.WriteAllTextDurable(Path.Combine(external, "keep.txt"), "K");

        var r = f.Service.MoveIntoZone(f.Zone, external, null, f.Commit);

        Assert.False(r.Success);
        Assert.Equal(ManagedError.NotUserDesktopItem, r.Error);
        Assert.Equal("K", f.Box.Fs.ReadAllText(Path.Combine(external, "keep.txt")));
        Assert.Empty(f.Zone.Items);
        Assert.Equal(0, f.CommitCalls);
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.Root));
    }

    [Fact]
    public void PublicDesktopItem_IsNeverMoved()
    {
        using var f = new ManagedFixture();
        var shared = Path.Combine(f.PublicDesktop, "Shared.lnk");
        f.Box.Fs.WriteAllTextDurable(shared, "S");

        var r = f.Service.MoveIntoZone(f.Zone, shared, null, f.Commit);

        Assert.Equal(ManagedError.NotUserDesktopItem, r.Error);
        Assert.Equal("S", f.Box.Fs.ReadAllText(shared));
    }

    [Fact]
    public void ItemNestedInADesktopSubfolder_IsNotAManagedCandidate()
    {
        using var f = new ManagedFixture();
        f.CreateDesktopFolder("Notatki", ("n.txt", "N"));

        var r = f.Service.MoveIntoZone(f.Zone, Path.Combine(f.Desktop, "Notatki", "n.txt"), null, f.Commit);

        Assert.Equal(ManagedError.NotUserDesktopItem, r.Error);
        Assert.Equal("N", f.Box.Fs.ReadAllText(Path.Combine(f.Desktop, "Notatki", "n.txt")));
    }

    [Fact]
    public void MissingItem_IsReported_NothingChanges()
    {
        using var f = new ManagedFixture();

        var r = f.Service.MoveIntoZone(f.Zone, f.OnDesktop("ghost.lnk"), null, f.Commit);

        Assert.Equal(ManagedError.NotFound, r.Error);
        Assert.Empty(f.PendingJournals());
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.Root));
    }

    [Fact]
    public void TwoItemsWithTheSameName_NeverCollide_EachGetsItsOwnStorageFolder()
    {
        using var f = new ManagedFixture();
        f.CreateDesktopFile("Game.lnk", "first");
        Assert.True(f.Service.MoveIntoZone(f.Zone, f.OnDesktop("Game.lnk"), null, f.Commit).Success);
        f.CreateDesktopFile("Game.lnk", "second"); // a new item with the same name appears on the Desktop later
        Assert.True(f.Service.MoveIntoZone(f.Zone, f.OnDesktop("Game.lnk"), null, f.Commit).Success);

        Assert.Equal(2, f.Zone.Items.Count);
        Assert.NotEqual(f.Zone.Items[0].Path, f.Zone.Items[1].Path);
        Assert.Equal("first", f.Box.Fs.ReadAllText(f.Zone.Items[0].Path));
        Assert.Equal("second", f.Box.Fs.ReadAllText(f.Zone.Items[1].Path));
    }

    [Fact]
    public void UnicodeAndSpaces_Survive()
    {
        using var f = new ManagedFixture("Użytkownik Müller — Größe łódź");
        var name = "Zażółć gęślą  jaźń 🦈 (kopia).lnk";
        var path = f.CreateDesktopFile(name, "ü");

        var r = f.Service.MoveIntoZone(f.Zone, path, "Zażółć 🦈", f.Commit);

        Assert.True(r.Success, r.Message);
        Assert.Equal(name, r.Item!.StoredName);
        Assert.Equal("ü", f.Box.Fs.ReadAllText(r.Item.Path));
        Assert.Equal("Zażółć 🦈", r.Item.Name);
    }

    [Fact]
    public void VeryLongPaths_AreHandled_FileAndFolder()
    {
        var deep = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat(new string('d', 40), 8));
        using var f = new ManagedFixture(deep);
        var file = f.CreateDesktopFile("long.lnk", "L");
        var folder = f.CreateDesktopFolder("long folder", ("inside.txt", "I"));
        Assert.True(file.Length > 300);

        Assert.True(f.Service.MoveIntoZone(f.Zone, file, null, f.Commit).Success);
        var r = f.Service.MoveIntoZone(f.Zone, folder, null, f.Commit);

        Assert.True(r.Success, r.Message);
        Assert.Equal("I", f.Box.Fs.ReadAllText(Path.Combine(r.Item!.Path, "inside.txt")));
        Assert.True(r.Item.Path.Length > 300);
    }
}

public class ManagedFailureTests
{
    [Fact]
    public void RenameFails_ItemStaysOnTheDesktop_NoJournalLeft_NoEmptyStorageFolder()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk", "X");
        f.Faulty.FailOn(nameof(IFileSystem.MoveEntry));

        var r = f.Service.MoveIntoZone(f.Zone, lnk, null, f.Commit);

        Assert.False(r.Success);
        Assert.Equal(ManagedError.IoFailure, r.Error);
        Assert.Equal("X", f.Box.Fs.ReadAllText(lnk));
        Assert.Empty(f.Zone.Items);
        Assert.Empty(f.PendingJournals());
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.ItemDirectory("item-001")));
        Assert.Equal(0, f.CommitCalls);
    }

    [Fact]
    public void JournalCannotBeWritten_NothingIsMoved()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk", "X");
        f.Faulty.FailOn(nameof(IFileSystem.WriteAllTextDurable)); // the very first journal write (Prepared)

        var r = f.Service.MoveIntoZone(f.Zone, lnk, null, f.Commit);

        Assert.Equal(ManagedError.JournalFailure, r.Error);
        Assert.Equal("X", f.Box.Fs.ReadAllText(lnk));
        Assert.Empty(f.Zone.Items);
    }

    [Fact]
    public void JournalMovingStateCannotBeWritten_NothingIsMoved_AndStorageFolderIsTidied()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk", "X");
        f.Faulty.FailOn(nameof(IFileSystem.WriteAllTextDurable), skip: 1); // Prepared ok, Moving fails

        var r = f.Service.MoveIntoZone(f.Zone, lnk, null, f.Commit);

        Assert.Equal(ManagedError.JournalFailure, r.Error);
        Assert.Equal("X", f.Box.Fs.ReadAllText(lnk));
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.ItemDirectory("item-001")));
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void ConfigSaveFails_AfterTheMove_ItemIsPutBackOnTheDesktop()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk", "X");
        f.CommitSucceeds = false;

        var r = f.Service.MoveIntoZone(f.Zone, lnk, null, f.Commit);

        Assert.False(r.Success);
        Assert.Equal(ManagedError.ConfigNotSaved, r.Error);
        Assert.Equal("X", f.Box.Fs.ReadAllText(lnk)); // back where it was
        Assert.Empty(f.Zone.Items);
        Assert.Empty(f.PendingJournals());
        Assert.False(f.Box.Fs.DirectoryExists(f.Paths.ItemDirectory("item-001")));
    }

    [Fact]
    public void ConfigSaveFails_AndRollbackIsImpossible_ItemStaysSafelyManaged_AndRecoveryCompletesItForward()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk", "X");
        f.CommitSucceeds = false;
        f.Faulty.FailOn(nameof(IFileSystem.MoveEntry), skip: 1); // the move succeeds, the rollback rename fails

        var r = f.Service.MoveIntoZone(f.Zone, lnk, "x", f.Commit);

        Assert.False(r.Success);
        Assert.Equal(ManagedError.ConfigNotSaved, r.Error);
        var stored = f.Paths.StoredPath("item-001", "x.lnk");
        Assert.Equal("X", f.Box.Fs.ReadAllText(stored)); // the single copy is safe in storage
        Assert.False(f.Box.Fs.PathExists(lnk));
        Assert.Single(f.PendingJournals()); // kept for recovery

        // Next start: a fresh config that does not know the item.
        var freshConfig = new AppConfig();
        var zone = ZoneCatalog.AddZone(freshConfig, "🎮 Gry", new(0, 0, 1920, 1040), 96);
        zone.Id = f.Zone.Id;
        f.CommitSucceeds = true;
        var actions = f.Service.Recover(freshConfig, f.Commit);

        var action = Assert.Single(actions);
        Assert.Equal(RecoveryOutcome.CompletedForward, action.Outcome);
        var item = Assert.Single(zone.Items);
        Assert.Equal(ItemKind.Managed, item.Kind);
        Assert.Equal("item-001", item.Id);
        Assert.Equal("X", f.Box.Fs.ReadAllText(item.Path));
        Assert.Empty(f.PendingJournals());
    }

    [Fact]
    public void CommitThatThrows_IsTreatedAsAFailure_NotACrash()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("x.lnk", "X");

        var r = f.Service.MoveIntoZone(f.Zone, lnk, null, () => throw new IOException("disk full"));

        Assert.Equal(ManagedError.ConfigNotSaved, r.Error);
        Assert.Equal("X", f.Box.Fs.ReadAllText(lnk));
    }

    [Fact]
    public void ItemOpenInAnotherProgram_CannotBeMoved_AndStaysPut()
    {
        using var f = new ManagedFixture();
        var lnk = f.CreateDesktopFile("locked.lnk", "L");
        using var hold = new FileStream(lnk, FileMode.Open, FileAccess.Read, FileShare.Read); // no FILE_SHARE_DELETE: blocks a rename

        var r = f.Service.MoveIntoZone(f.Zone, lnk, null, f.Commit);

        Assert.False(r.Success);
        Assert.Equal(ManagedError.IoFailure, r.Error);
        hold.Dispose();
        Assert.Equal("L", f.Box.Fs.ReadAllText(lnk));
        Assert.Empty(f.PendingJournals());
    }
}
