using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class ZoneItemsTests
{
    private static ZoneConfig NewZone() => new() { Title = "🦈 NextMind" };

    [Fact]
    public void Add_StoresNormalisedPath_AndName_InOrder()
    {
        using var box = new TempSandbox();
        var zone = NewZone();
        var a = box.Combine("a.txt");
        var b = box.Combine("b.txt");
        var c = box.Combine("c.txt");

        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, a, "Alpha", out _));
        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, b, null, out _));
        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, c, "  Gamma  ", out _));

        Assert.Equal([a, b, c], zone.Items.Select(i => i.Path));
        Assert.Equal(["Alpha", "b.txt", "Gamma"], zone.Items.Select(i => i.Name));
        Assert.Equal(3, zone.Items.Select(i => i.Id).Distinct().Count());
    }

    [Fact]
    public void Duplicate_SameTarget_IsRecognised_EvenWithCaseTrailingSlashOrDotDot()
    {
        using var box = new TempSandbox();
        var zone = NewZone();
        var dir = box.Combine("Projekt Źródło");
        box.Fs.CreateDirectory(dir);

        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, dir, null, out var first));

        Assert.Equal(AddItemResult.Duplicate, ZoneItems.TryAdd(zone, dir.ToUpperInvariant(), null, out var d1));
        Assert.Equal(AddItemResult.Duplicate, ZoneItems.TryAdd(zone, dir + Path.DirectorySeparatorChar, null, out var d2));
        Assert.Equal(AddItemResult.Duplicate, ZoneItems.TryAdd(zone, Path.Combine(dir, "..", "Projekt Źródło"), null, out var d3));

        Assert.Single(zone.Items);
        Assert.Same(first, d1);
        Assert.Same(first, d2);
        Assert.Same(first, d3);
    }

    [Fact]
    public void SameTarget_CanLiveInTwoZones()
    {
        using var box = new TempSandbox();
        var z1 = NewZone();
        var z2 = NewZone();
        var target = box.Combine("shared.txt");

        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(z1, target, null, out _));
        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(z2, target, null, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative\\path.txt")]
    [InlineData("C:relative.txt")]
    [InlineData("..\\up.txt")]
    [InlineData("C:\\bad|name?.txt")]
    public void Invalid_Paths_AreRejected_AndNothingIsAdded(string? path)
    {
        var zone = NewZone();

        Assert.Equal(AddItemResult.Invalid, ZoneItems.TryAdd(zone, path, null, out var item));
        Assert.Null(item);
        Assert.Empty(zone.Items);
    }

    [Fact]
    public void Unicode_Spaces_AndLongPaths_AreAccepted()
    {
        using var box = new TempSandbox("Użytkownik Müller — Größe łódź");
        var zone = NewZone();
        var names = new[] { "Zażółć gęślą jaźń 🦈.txt", "plik ze spacjami  i  kropkami...txt", "Größe-Übung ß.docx", "日本語.txt" };

        foreach (var n in names)
        {
            var path = box.Combine(n);
            Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, path, null, out _));
        }

        var deep = Path.Combine(box.Root, string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat(new string('d', 40), 9)), "long.txt");
        Assert.True(deep.Length > 300);
        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, deep, null, out _));

        Assert.Equal(names, zone.Items.Take(4).Select(i => i.Name));
        Assert.Equal(5, zone.Items.Count);
    }

    [Fact]
    public void Remove_DeletesOnlyTheReference_AndKeepsOrder_AndTargetUntouched()
    {
        using var box = new TempSandbox();
        var zone = NewZone();
        var files = new[] { "one.txt", "two.txt", "three.txt" }.Select(n => box.Combine(n)).ToArray();
        foreach (var f in files)
        {
            box.Fs.WriteAllTextDurable(f, "KEEP ME " + Path.GetFileName(f));
            ZoneItems.TryAdd(zone, f, null, out _);
        }

        var middle = zone.Items[1];
        Assert.True(ZoneItems.Remove(zone, middle.Id));
        Assert.False(ZoneItems.Remove(zone, middle.Id)); // already gone

        Assert.Equal([files[0], files[2]], zone.Items.Select(i => i.Path));
        foreach (var f in files)
        {
            Assert.True(box.Fs.FileExists(f));
            Assert.Equal("KEEP ME " + Path.GetFileName(f), box.Fs.ReadAllText(f));
        }
    }

    [Fact]
    public void MissingTarget_IsReportedMissing_ButStaysInTheZone()
    {
        using var box = new TempSandbox();
        var zone = NewZone();
        var present = box.Combine("here.txt");
        var gone = box.Combine("gone.txt");
        box.Fs.WriteAllTextDurable(present, "x");
        ZoneItems.TryAdd(zone, present, null, out var p);
        ZoneItems.TryAdd(zone, gone, null, out var g);

        var probe = new RealPathProbe();

        Assert.Equal(ItemAvailability.Available, ItemProbe.Check(probe, p!));
        Assert.Equal(ItemAvailability.Missing, ItemProbe.Check(probe, g!));
        Assert.Equal(2, zone.Items.Count); // nothing is auto-removed
    }

    [Fact]
    public void FolderTarget_IsAvailable()
    {
        using var box = new TempSandbox();
        var dir = box.Combine("folder");
        box.Fs.CreateDirectory(dir);
        ZoneItems.TryAdd(NewZone(), dir, null, out var item);

        Assert.Equal(ItemAvailability.Available, ItemProbe.Check(new RealPathProbe(), item!));
    }

    [Fact]
    public void Probe_DoesNotThrow_OnGarbagePaths()
    {
        var probe = new RealPathProbe();
        Assert.False(probe.Exists("C:\\bad|name?.txt"));
        Assert.False(probe.Exists(new string('x', 5000)));
    }

    [Fact]
    public void DriveRoot_GetsItsPathAsName()
    {
        var zone = NewZone();
        var root = Path.GetPathRoot(Path.GetTempPath())!;

        ZoneItems.TryAdd(zone, root, null, out var item);

        Assert.Equal(root, item!.Name);
    }

    [Fact]
    public void ZoneIsCappedAt_MaxItems()
    {
        var zone = NewZone();
        for (var i = 0; i < ZoneLimits.MaxItemsPerZone; i++)
        {
            Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(zone, $"C:\\ref\\item{i}.txt", null, out _));
        }

        Assert.Equal(AddItemResult.Invalid, ZoneItems.TryAdd(zone, "C:\\ref\\one-too-many.txt", null, out _));
    }

    [Fact]
    public void Items_PersistThroughConfigStore_WithOrderIdsAndUnicode()
    {
        using var box = new TempSandbox();
        var cfg = new AppConfig();
        var zone = ZoneCatalog.AddZone(cfg, "🛠 Narzędzia", new(0, 0, 1920, 1040), 96);
        var targets = new[] { "z-pierwszy 🦈.txt", "a drugi ąę.txt", "m trzeci.txt" }.Select(n => box.Combine(n)).ToArray();
        foreach (var t in targets)
        {
            ZoneItems.TryAdd(zone, t, null, out _);
        }

        var store = new ConfigStore(box.Fs, box.Combine("cfg"));
        store.Load();
        Assert.True(store.Save(cfg));

        var back = new ConfigStore(box.Fs, box.Combine("cfg")).Load().Config;

        var z = Assert.Single(back.Zones);
        Assert.Equal("🛠 Narzędzia", z.Title);
        Assert.Equal(targets, z.Items.Select(i => i.Path)); // exact order kept, not sorted
        Assert.Equal(zone.Items.Select(i => i.Id), z.Items.Select(i => i.Id));
        Assert.Equal(["z-pierwszy 🦈.txt", "a drugi ąę.txt", "m trzeci.txt"], z.Items.Select(i => i.Name));
    }

    [Fact]
    public void LoadedConfig_WithDuplicatesAndGarbageItems_IsCleaned_WithoutLosingValidOnes()
    {
        const string json = """
            { "schemaVersion": 2, "zones": [ { "id": "z", "title": "T", "items": [
                { "id": "1", "path": "C:\\data\\a.txt", "name": "A" },
                { "id": "1", "path": "C:\\data\\B.txt", "name": "" },
                { "id": "3", "path": "c:\\DATA\\A.TXT", "name": "dup of A" },
                { "id": "4", "path": "relative.txt", "name": "bad" },
                { "id": "5", "path": "", "name": "empty" },
                null
            ] } ] }
            """;

        var cfg = ConfigSerializer.Deserialize(json);

        var items = cfg.Zones[0].Items;
        Assert.Equal(2, items.Count);
        Assert.Equal(@"C:\data\a.txt", items[0].Path);
        Assert.Equal(@"C:\data\B.txt", items[1].Path);
        Assert.Equal("B.txt", items[1].Name); // empty name falls back to the file name
        Assert.NotEqual(items[0].Id, items[1].Id); // duplicate id repaired
    }
}
