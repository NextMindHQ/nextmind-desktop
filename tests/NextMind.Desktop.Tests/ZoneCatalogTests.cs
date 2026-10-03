using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class ZoneCatalogTests
{
    private static readonly RectPx Work = new(0, 0, 1920, 1040);

    [Fact]
    public void MultipleZones_GetUniqueIds_AndCascadingPositions_AndEmojiNames()
    {
        var cfg = new AppConfig();
        var names = new[] { "🦈 NextMind", "🎮 Gry", "🛠 Narzędzia", "📁 Robocze" };

        var zones = names.Select(n => ZoneCatalog.AddZone(cfg, n, Work, 96)).ToList();

        Assert.Equal(4, cfg.Zones.Count);
        Assert.Equal(names, cfg.Zones.Select(z => z.Title));
        Assert.Equal(4, zones.Select(z => z.Id).Distinct().Count());
        Assert.Equal(4, zones.Select(z => (z.X, z.Y)).Distinct().Count());
        Assert.All(zones, z => Assert.InRange(z.X, Work.X, Work.Right - z.Width));
    }

    [Fact]
    public void NewZone_HasTheSameCapabilitiesAsTheFirst_ItemsHiddenCollapsed()
    {
        var cfg = new AppConfig();
        var first = ZoneCatalog.AddZone(cfg, "🦈 NextMind", Work, 96);
        var second = ZoneCatalog.AddZone(cfg, "🎮 Gry", Work, 96);

        Assert.Equal(first.GetType(), second.GetType());
        Assert.Empty(second.Items);
        Assert.False(second.Hidden);
        Assert.False(second.Collapsed);
        Assert.Equal((first.Width, first.Height), (second.Width, second.Height));
        Assert.Equal(AddItemResult.Added, ZoneItems.TryAdd(second, @"C:\x\y.txt", null, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void EmptyName_FallsBackToTheDefaultTitle(string? name)
    {
        var z = ZoneCatalog.AddZone(new AppConfig(), name, Work, 96);
        Assert.Equal(ZoneLimits.DefaultTitle, z.Title);
    }

    [Fact]
    public void Rename_ChangesOnlyTheTitle_AndKeepsItems()
    {
        var cfg = new AppConfig();
        var z = ZoneCatalog.AddZone(cfg, "Old", Work, 96);
        ZoneItems.TryAdd(z, @"C:\x\keep.txt", null, out _);

        Assert.True(ZoneCatalog.Rename(cfg, z.Id, "  🛠 Narzędzia  "));

        Assert.Equal("🛠 Narzędzia", z.Title);
        Assert.Single(z.Items);
    }

    [Fact]
    public void Rename_RejectsEmpty_AndUnknownZone_AndTruncatesLongNames()
    {
        var cfg = new AppConfig();
        var z = ZoneCatalog.AddZone(cfg, "Name", Work, 96);

        Assert.False(ZoneCatalog.Rename(cfg, z.Id, "   "));
        Assert.False(ZoneCatalog.Rename(cfg, "no-such-zone", "X"));
        Assert.Equal("Name", z.Title);

        Assert.True(ZoneCatalog.Rename(cfg, z.Id, new string('x', 500)));
        Assert.Equal(ZoneLimits.MaxTitleLength, z.Title.Length);
    }

    [Fact]
    public void DeleteZone_RemovesConfigurationOnly_TargetFilesAndFoldersAreUntouched()
    {
        using var box = new TempSandbox();
        var file = box.Combine("important ąę.docx");
        var folder = box.Combine("Projekt 🦈");
        var inner = Path.Combine(folder, "inner.txt");
        box.Fs.WriteAllTextDurable(file, "PRECIOUS");
        box.Fs.CreateDirectory(folder);
        box.Fs.WriteAllTextDurable(inner, "ALSO PRECIOUS");

        var cfg = new AppConfig();
        var keep = ZoneCatalog.AddZone(cfg, "Keep", Work, 96);
        var doomed = ZoneCatalog.AddZone(cfg, "Doomed", Work, 96);
        ZoneItems.TryAdd(doomed, file, null, out _);
        ZoneItems.TryAdd(doomed, folder, null, out _);

        Assert.True(ZoneCatalog.Delete(cfg, doomed.Id));

        Assert.Equal([keep.Id], cfg.Zones.Select(z => z.Id));
        Assert.True(box.Fs.FileExists(file));
        Assert.Equal("PRECIOUS", box.Fs.ReadAllText(file));
        Assert.True(box.Fs.DirectoryExists(folder));
        Assert.Equal("ALSO PRECIOUS", box.Fs.ReadAllText(inner));
    }

    [Fact]
    public void DeleteZone_PersistsWithoutTheZone_AndWithoutTouchingTargets()
    {
        using var box = new TempSandbox();
        var target = box.Combine("t.txt");
        box.Fs.WriteAllTextDurable(target, "T");

        var cfg = new AppConfig();
        var a = ZoneCatalog.AddZone(cfg, "A", Work, 96);
        var b = ZoneCatalog.AddZone(cfg, "B", Work, 96);
        ZoneItems.TryAdd(a, target, null, out _);
        var store = new ConfigStore(box.Fs, box.Combine("cfg"));
        store.Load();
        store.Save(cfg);

        ZoneCatalog.Delete(cfg, a.Id);
        store.Save(cfg);

        var back = new ConfigStore(box.Fs, box.Combine("cfg")).Load().Config;
        Assert.Equal([b.Id], back.Zones.Select(z => z.Id));
        Assert.Equal("T", box.Fs.ReadAllText(target));
    }

    [Fact]
    public void Delete_UnknownZone_ReturnsFalse_AndChangesNothing()
    {
        var cfg = new AppConfig();
        ZoneCatalog.AddZone(cfg, "A", Work, 96);

        Assert.False(ZoneCatalog.Delete(cfg, "nope"));
        Assert.Single(cfg.Zones);
    }

    [Fact]
    public void HideZone_KeepsItemsAndConfig_AndOnlyThatZoneIsHidden()
    {
        var cfg = new AppConfig();
        var a = ZoneCatalog.AddZone(cfg, "A", Work, 96);
        var b = ZoneCatalog.AddZone(cfg, "B", Work, 96);
        ZoneItems.TryAdd(a, @"C:\x\a.txt", null, out _);

        Assert.True(ZoneCatalog.SetHidden(cfg, a.Id, true));

        Assert.False(ZoneCatalog.IsVisible(cfg, a));
        Assert.True(ZoneCatalog.IsVisible(cfg, b));
        Assert.Equal(2, cfg.Zones.Count);
        Assert.Single(a.Items);
    }

    [Fact]
    public void ShowHideToggle_HidesAll_ThenShowsAll_IncludingIndividuallyHiddenZones()
    {
        var cfg = new AppConfig();
        var a = ZoneCatalog.AddZone(cfg, "A", Work, 96);
        var b = ZoneCatalog.AddZone(cfg, "B", Work, 96);
        ZoneCatalog.SetHidden(cfg, a.Id, true); // hidden with its ×

        // Something is still visible (B) -> the tray toggle hides everything.
        Assert.False(ZoneCatalog.ToggleAll(cfg));
        Assert.False(ZoneCatalog.IsVisible(cfg, a));
        Assert.False(ZoneCatalog.IsVisible(cfg, b));

        // Nothing visible -> the tray toggle brings everything back, A included.
        Assert.True(ZoneCatalog.ToggleAll(cfg));
        Assert.True(ZoneCatalog.IsVisible(cfg, a));
        Assert.True(ZoneCatalog.IsVisible(cfg, b));
    }

    [Fact]
    public void WhenOnlyCloseButtonHidZones_TrayToggleRestoresThem()
    {
        var cfg = new AppConfig();
        var a = ZoneCatalog.AddZone(cfg, "A", Work, 96);
        ZoneCatalog.SetHidden(cfg, a.Id, true);

        Assert.True(ZoneCatalog.ToggleAll(cfg));

        Assert.True(ZoneCatalog.IsVisible(cfg, a));
    }

    [Fact]
    public void HiddenAndVisibleState_Persist()
    {
        using var box = new TempSandbox();
        var cfg = new AppConfig();
        var a = ZoneCatalog.AddZone(cfg, "A", Work, 96);
        ZoneCatalog.AddZone(cfg, "B", Work, 96);
        ZoneCatalog.SetHidden(cfg, a.Id, true);
        cfg.ZonesVisible = true;
        cfg.AutostartDecided = true;

        var store = new ConfigStore(box.Fs, box.Combine("cfg"));
        store.Load();
        store.Save(cfg);
        var back = new ConfigStore(box.Fs, box.Combine("cfg")).Load().Config;

        Assert.True(back.Zones[0].Hidden);
        Assert.False(back.Zones[1].Hidden);
        Assert.True(back.AutostartDecided);
    }

    [Fact]
    public void ConfigFromTheShippedM1_V1_LoadsAndIsUpgradedToTheCurrentSchema()
    {
        const string v1 = """
            { "schemaVersion": 1, "zonesVisible": true,
              "zones": [ { "id": "abc", "title": "\uD83E\uDD88 NextMind", "x": 899, "y": 381, "width": 476, "height": 344, "collapsed": false, "dpi": 96 } ] }
            """;

        var parsed = ConfigMigrator.Parse(v1);

        Assert.True(parsed.Migrated);
        Assert.Equal(ConfigSchema.Current, parsed.Config.SchemaVersion);
        var z = Assert.Single(parsed.Config.Zones);
        Assert.Equal("🦈 NextMind", z.Title);
        Assert.Empty(z.Items);
        Assert.False(z.Hidden);
        Assert.False(parsed.Config.AutostartDecided);
    }
}
