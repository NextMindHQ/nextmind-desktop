using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class SortingTests
{
    private static ZoneConfig ZoneWith(params (string Name, string Path, bool? Folder, int Day)[] items)
    {
        var zone = new ZoneConfig { Title = "Z" };
        foreach (var (name, path, folder, day) in items)
        {
            zone.Items.Add(new ItemConfig { Name = name, Path = path, IsFolder = folder, AddedAtUtc = new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc) });
        }

        return zone;
    }

    private static readonly (string, string, bool?, int)[] Sample =
    [
        ("Path of Exile", @"C:\x\Path of Exile.lnk", false, 5),
        ("battlefield", @"C:\x\battlefield.lnk", false, 1),
        ("Źródła", @"C:\x\Zrodla", true, 9),
        ("League", @"C:\x\League.exe", false, 3),
        ("notatki.txt", @"C:\x\notatki.txt", false, 7),
    ];

    private static string[] Names(ZoneConfig z, SortMode mode) => ZoneSorter.Order(z.Items, mode).Select(i => i.Name).ToArray();

    [Fact]
    public void Custom_KeepsTheStoredOrder()
        => Assert.Equal(["Path of Exile", "battlefield", "Źródła", "League", "notatki.txt"], Names(ZoneWith(Sample), SortMode.Custom));

    [Fact]
    public void NameAscending_IsCaseInsensitive()
        => Assert.Equal(["battlefield", "League", "notatki.txt", "Path of Exile", "Źródła"], Names(ZoneWith(Sample), SortMode.NameAscending));

    [Fact]
    public void NameDescending_IsTheReverse()
        => Assert.Equal(["Źródła", "Path of Exile", "notatki.txt", "League", "battlefield"], Names(ZoneWith(Sample), SortMode.NameDescending));

    [Fact]
    public void AddedNewest_UsesWhenItWasAdded_NotTheFilesystem()
        => Assert.Equal(["Źródła", "notatki.txt", "Path of Exile", "League", "battlefield"], Names(ZoneWith(Sample), SortMode.AddedNewest));

    [Fact]
    public void AddedOldest_IsTheReverse()
        => Assert.Equal(["battlefield", "League", "Path of Exile", "notatki.txt", "Źródła"], Names(ZoneWith(Sample), SortMode.AddedOldest));

    [Fact]
    public void Type_GroupsFolderShortcutApplicationDocument_ThenSortsByName()
        => Assert.Equal(["Źródła", "battlefield", "Path of Exile", "League", "notatki.txt"], Names(ZoneWith(Sample), SortMode.Type));

    [Fact]
    public void SortingNeverReordersTheStoredList()
    {
        var zone = ZoneWith(Sample);
        var before = zone.Items.Select(i => i.Id).ToArray();

        foreach (var mode in Enum.GetValues<SortMode>())
        {
            ZoneSorter.Order(zone.Items, mode);
        }

        ZoneOrdering.SetSortMode(zone, SortMode.NameDescending);
        Assert.Equal(before, zone.Items.Select(i => i.Id));
        Assert.Equal(SortMode.NameDescending, zone.SortMode);
    }

    [Fact]
    public void Sorts_AreStable_TiesKeepTheCustomOrder()
    {
        var zone = ZoneWith(("same", @"C:\a\1", false, 1), ("same", @"C:\a\2", false, 1), ("same", @"C:\a\3", false, 1));

        Assert.Equal(["1", "2", "3"], ZoneSorter.Order(zone.Items, SortMode.NameAscending).Select(i => Path.GetFileName(i.Path)));
        Assert.Equal(["1", "2", "3"], ZoneSorter.Order(zone.Items, SortMode.AddedOldest).Select(i => Path.GetFileName(i.Path)));
        Assert.Equal(["3", "2", "1"], ZoneSorter.Order(zone.Items, SortMode.AddedNewest).Select(i => Path.GetFileName(i.Path)));
    }

    [Theory]
    [InlineData("a.lnk", false, ItemTypeCategory.Shortcut)]
    [InlineData("a.URL", false, ItemTypeCategory.Shortcut)]
    [InlineData("a.exe", false, ItemTypeCategory.Application)]
    [InlineData("a.pdf", false, ItemTypeCategory.Document)]
    [InlineData("a.JPG", false, ItemTypeCategory.Image)]
    [InlineData("a.7z", false, ItemTypeCategory.Archive)]
    [InlineData("a.xyz", false, ItemTypeCategory.Other)]
    [InlineData("noext", false, ItemTypeCategory.Other)]
    [InlineData("anything.v2", true, ItemTypeCategory.Folder)]
    public void TypeClassification(string name, bool folder, ItemTypeCategory expected)
        => Assert.Equal(expected, ItemTypes.Classify(@"C:\x\" + name, folder));

    [Fact]
    public void OldItemsWithoutAFolderFlag_AreGuessedUntilProbed()
    {
        Assert.Equal(ItemTypeCategory.Folder, ItemTypes.Classify(new ItemConfig { Path = @"C:\x\Projekty" }));
        Assert.Equal(ItemTypeCategory.Document, ItemTypes.Classify(new ItemConfig { Path = @"C:\x\a.txt" }));
        Assert.Equal(ItemTypeCategory.Other, ItemTypes.Classify(new ItemConfig { Path = @"C:\x\Projekty", IsFolder = false }));
    }
}

public class OrderingTests
{
    private static ZoneConfig Zone(params string[] names)
    {
        var zone = new ZoneConfig();
        var day = 1;
        foreach (var n in names)
        {
            ZoneItems.TryAdd(zone, @"C:\x\" + n, n, ItemKind.Reference, false, new DateTime(2026, 1, day++, 0, 0, 0, DateTimeKind.Utc), null, out _);
        }

        return zone;
    }

    private static string[] Order(ZoneConfig z) => z.Items.Select(i => i.Name).ToArray();

    private static string Id(ZoneConfig z, string name) => z.Items.First(i => i.Name == name).Id;

    [Fact]
    public void Drag_BattlefieldPoeLeague_BecomesPoeLeagueBattlefield()
    {
        var z = Zone("Battlefield", "Path of Exile", "League");

        Assert.True(ZoneOrdering.MoveItem(z, Id(z, "Battlefield"), 2));

        Assert.Equal(["Path of Exile", "League", "Battlefield"], Order(z));
        Assert.Equal(SortMode.Custom, z.SortMode);
    }

    [Theory]
    [InlineData("C", 0, new[] { "C", "A", "B", "D" })]
    [InlineData("A", 3, new[] { "B", "C", "D", "A" })]
    [InlineData("B", 1, new[] { "A", "B", "C", "D" })]
    [InlineData("D", 99, new[] { "A", "B", "C", "D" })]
    [InlineData("A", -5, new[] { "A", "B", "C", "D" })]
    public void MoveItem_ToAnyIndex_IsClamped(string item, int index, string[] expected)
    {
        var z = Zone("A", "B", "C", "D");
        ZoneOrdering.MoveItem(z, Id(z, item), index);
        Assert.Equal(expected, Order(z));
    }

    [Fact]
    public void Dragging_InASortedZone_SwitchesToCustom_AndKeepsWhatTheUserSaw()
    {
        var z = Zone("Charlie", "Alpha", "Bravo");
        ZoneOrdering.SetSortMode(z, SortMode.NameAscending); // displayed: Alpha, Bravo, Charlie

        ZoneOrdering.MoveItem(z, Id(z, "Charlie"), 0); // user drags Charlie to the front of what they SEE

        Assert.Equal(SortMode.Custom, z.SortMode);
        Assert.Equal(["Charlie", "Alpha", "Bravo"], Order(z));
        Assert.Equal(["Charlie", "Alpha", "Bravo"], ZoneSorter.Order(z).Select(i => i.Name));
    }

    [Fact]
    public void UnknownItem_ChangesNothing()
    {
        var z = Zone("A", "B");
        Assert.False(ZoneOrdering.MoveItem(z, "nope", 0));
        Assert.Equal(["A", "B"], Order(z));
    }

    [Fact]
    public void SwitchingBackToCustom_RestoresTheLastCustomOrder()
    {
        var z = Zone("C", "A", "B");
        ZoneOrdering.SetSortMode(z, SortMode.NameAscending);
        ZoneOrdering.SetSortMode(z, SortMode.Custom);

        Assert.Equal(["C", "A", "B"], ZoneSorter.Order(z).Select(i => i.Name));
    }

    [Fact]
    public void InvalidSortMode_FallsBackToCustom()
    {
        var z = Zone("A");
        ZoneOrdering.SetSortMode(z, (SortMode)99);
        Assert.Equal(SortMode.Custom, z.SortMode);
    }

    [Fact]
    public void OrderAndSortMode_Persist_ThroughTheConfigStore()
    {
        using var box = new TempSandbox();
        var cfg = new AppConfig();
        var zone = ZoneCatalog.AddZone(cfg, "🎮 Gry", new RectPx(0, 0, 1920, 1040), 96);
        foreach (var n in new[] { "Battlefield", "Path of Exile", "League" })
        {
            ZoneItems.TryAdd(zone, @"C:\x\" + n + ".lnk", n, out _);
        }

        ZoneOrdering.MoveItem(zone, zone.Items[0].Id, 2);
        var other = ZoneCatalog.AddZone(cfg, "🛠 Narzędzia", new RectPx(0, 0, 1920, 1040), 96);
        ZoneOrdering.SetSortMode(other, SortMode.AddedNewest);

        var store = new ConfigStore(box.Fs, box.Combine("cfg"));
        store.Load();
        store.Save(cfg);
        var back = new ConfigStore(box.Fs, box.Combine("cfg")).Load().Config;

        Assert.Equal(["Path of Exile", "League", "Battlefield"], back.Zones[0].Items.Select(i => i.Name));
        Assert.Equal(SortMode.Custom, back.Zones[0].SortMode);
        Assert.Equal(SortMode.AddedNewest, back.Zones[1].SortMode);
        Assert.Contains("\"sortMode\": \"addedNewest\"", ConfigSerializer.Serialize(back)); // readable in the file
    }
}

public class M3ConfigMigrationTests
{
    private const string M2Config = """
        {
          "schemaVersion": 2, "zonesVisible": true, "autostartDecided": true,
          "zones": [
            { "id": "zone-gry", "title": "🎮 Gry", "x": 1500, "y": 120, "width": 400, "height": 300, "collapsed": false, "dpi": 96, "hidden": false,
              "items": [
                { "id": "i1", "path": "C:\\Games\\Battlefield 6\\bf6.exe", "name": "Battlefield 6" },
                { "id": "i2", "path": "C:\\Games\\Path of Exile", "name": "Path of Exile" },
                { "id": "i3", "path": "C:\\Games\\League.lnk", "name": "League" } ] } ]
        }
        """;

    [Fact]
    public void M2Config_Loads_WithNothingLost_AndEveryItemStaysAReference()
    {
        var parsed = ConfigMigrator.Parse(M2Config);

        Assert.True(parsed.Migrated);
        Assert.Equal(ConfigSchema.Current, parsed.Config.SchemaVersion);
        Assert.True(parsed.Config.AutostartDecided);
        Assert.False(parsed.Config.ManagedDesktopMovesEnabled); // the safety gate is OFF after migration
        var z = Assert.Single(parsed.Config.Zones);
        Assert.Equal(("zone-gry", "🎮 Gry", 1500, 120, 400, 300), (z.Id, z.Title, z.X, z.Y, z.Width, z.Height));
        Assert.Equal(SortMode.Custom, z.SortMode);
        Assert.Equal(["Battlefield 6", "Path of Exile", "League"], z.Items.Select(i => i.Name));
        Assert.All(z.Items, i => { Assert.Equal(ItemKind.Reference, i.Kind); Assert.Null(i.StoredName); });
    }

    [Fact]
    public void AddedAtFallback_IsDeterministic_EpochPlusPosition_AndStableAcrossLoads()
    {
        var first = ConfigMigrator.Parse(M2Config).Config.Zones[0].Items.Select(i => i.AddedAtUtc).ToArray();
        var second = ConfigMigrator.Parse(M2Config).Config.Zones[0].Items.Select(i => i.AddedAtUtc).ToArray();

        Assert.Equal(first, second);
        Assert.Equal([DateTime.UnixEpoch, DateTime.UnixEpoch.AddSeconds(1), DateTime.UnixEpoch.AddSeconds(2)], first);
        Assert.All(first, d => Assert.Equal(DateTimeKind.Utc, d!.Value.Kind));
    }

    [Fact]
    public void ItemsAddedAfterMigration_AreNewerThanTheMigratedOnes()
    {
        var zone = ConfigMigrator.Parse(M2Config).Config.Zones[0];

        ZoneItems.TryAdd(zone, @"C:\Games\New.lnk", "New", out _);

        Assert.Equal("New", ZoneSorter.Order(zone.Items, SortMode.AddedNewest)[0].Name);
        Assert.Equal("Battlefield 6", ZoneSorter.Order(zone.Items, SortMode.AddedOldest)[0].Name);
    }

    [Fact]
    public void ExistingAddedAt_IsPreserved_AndRoundTrips()
    {
        var stamp = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);
        var cfg = new AppConfig();
        var zone = ZoneCatalog.AddZone(cfg, "Z", new RectPx(0, 0, 100, 100), 96);
        ZoneItems.TryAdd(zone, @"C:\x\a.txt", "a", ItemKind.Reference, false, stamp, null, out _);

        var back = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(cfg));

        Assert.Equal(stamp, back.Zones[0].Items[0].AddedAtUtc);
    }

    [Fact]
    public void ManagedItems_RoundTrip_WithKindAndStoredName()
    {
        var cfg = new AppConfig();
        var zone = ZoneCatalog.AddZone(cfg, "Z", new RectPx(0, 0, 100, 100), 96);
        ZoneItems.TryAdd(zone, @"C:\App\ManagedItems\id-1\Game.lnk", "Game", ItemKind.Managed, false, DateTime.UtcNow, "Game.lnk", out _);

        var json = ConfigSerializer.Serialize(cfg);
        var item = ConfigSerializer.Deserialize(json).Zones[0].Items[0];

        Assert.Contains("\"kind\": \"managed\"", json);
        Assert.Equal(ItemKind.Managed, item.Kind);
        Assert.Equal("Game.lnk", item.StoredName);
    }

    [Fact]
    public void M1AndM2Configs_StillLoad()
    {
        const string m1 = """{ "schemaVersion": 1, "zones": [ { "id": "a", "title": "T", "x": 1, "y": 2, "width": 300, "height": 200 } ] }""";
        Assert.True(ConfigMigrator.Parse(m1).Migrated);
        Assert.Single(ConfigMigrator.Parse(m1).Config.Zones);
    }

    [Fact]
    public void GarbageEnumValues_FallBackToSafeDefaults()
    {
        const string json = """{ "schemaVersion": 3, "zones": [ { "id": "a", "title": "T", "sortMode": 99, "items": [ { "id": "1", "path": "C:\\a.txt", "kind": 7 } ] } ] }""";

        var z = ConfigSerializer.Deserialize(json).Zones[0];

        Assert.Equal(SortMode.Custom, z.SortMode);
        Assert.Equal(ItemKind.Reference, z.Items[0].Kind);
    }

    [Fact]
    public void ManagedItemWithoutAStoredName_GetsOneFromItsPath()
    {
        const string json = """{ "schemaVersion": 3, "zones": [ { "id": "a", "title": "T", "items": [ { "id": "1", "path": "C:\\S\\1\\x.lnk", "kind": "managed" } ] } ] }""";
        Assert.Equal("x.lnk", ConfigSerializer.Deserialize(json).Zones[0].Items[0].StoredName);
    }
}
