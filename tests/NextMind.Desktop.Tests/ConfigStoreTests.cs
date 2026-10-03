using System.Text;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class ConfigStoreTests
{
    private static AppConfig OneZone(string title = "🦈 NextMind", int x = 100) => new()
    {
        Zones = [new ZoneConfig { Id = "z1", Title = title, X = x, Y = 200, Width = 333, Height = 222, Collapsed = true, Dpi = 120 }],
    };

    private static ConfigStore NewStore(TempSandbox box, IFileSystem? fs = null, Func<DateTime>? clock = null)
        => new(fs ?? box.Fs, box.Combine("cfg"), clock: clock);

    [Fact]
    public void Load_WithNothing_CreatesDefaultsInMemory_AndWritesNothing()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.Created, r.Status);
        Assert.Empty(r.Config.Zones);
        Assert.False(box.Fs.FileExists(store.MainPath));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_ZonePersistence()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();

        Assert.True(store.Save(OneZone()));
        var r = NewStore(box).Load();

        Assert.Equal(ConfigLoadStatus.Loaded, r.Status);
        var z = Assert.Single(r.Config.Zones);
        Assert.Equal(("z1", "🦈 NextMind", 100, 200, 333, 222, true, 120), (z.Id, z.Title, z.X, z.Y, z.Width, z.Height, z.Collapsed, z.Dpi));
    }

    [Fact]
    public void SecondSave_KeepsPreviousGeneration_AsBak_AndLeavesNoTemp()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();

        store.Save(OneZone(x: 1));
        store.Save(OneZone(x: 2));

        Assert.True(box.Fs.FileExists(store.BackupPath));
        Assert.False(box.Fs.FileExists(store.TempPath));
        Assert.Equal(2, ConfigSerializer.Deserialize(box.Fs.ReadAllText(store.MainPath)).Zones[0].X);
        Assert.Equal(1, ConfigSerializer.Deserialize(box.Fs.ReadAllText(store.BackupPath)).Zones[0].X);
    }

    [Fact]
    public void FailedTempWrite_LeavesExistingConfigIntact()
    {
        using var box = new TempSandbox();
        var faulty = new FaultInjectingFileSystem(box.Fs);
        var store = NewStore(box, faulty);
        store.Load();
        store.Save(OneZone(x: 1));

        faulty.FailOn(nameof(IFileSystem.WriteAllTextDurable));
        Assert.Throws<IOException>(() => store.Save(OneZone(x: 2)));

        var r = NewStore(box).Load();
        Assert.Equal(ConfigLoadStatus.Loaded, r.Status);
        Assert.Equal(1, r.Config.Zones[0].X);
    }

    [Fact]
    public void FailedReplace_LeavesExistingConfigIntact_AndCleansTemp()
    {
        using var box = new TempSandbox();
        var faulty = new FaultInjectingFileSystem(box.Fs);
        var store = NewStore(box, faulty);
        store.Load();
        store.Save(OneZone(x: 1));

        faulty.FailOn(nameof(IFileSystem.ReplaceFile));
        Assert.Throws<IOException>(() => store.Save(OneZone(x: 2)));

        Assert.False(box.Fs.FileExists(store.TempPath));
        Assert.Equal(1, ConfigSerializer.Deserialize(box.Fs.ReadAllText(store.MainPath)).Zones[0].X);
    }

    [Fact]
    public void CorruptMain_RecoversFromBackup_AndPreservesCorruptFile()
    {
        using var box = new TempSandbox();
        var store = NewStore(box, clock: () => new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc));
        store.Load();
        store.Save(OneZone(x: 1));
        store.Save(OneZone(x: 2));
        box.Fs.WriteAllTextDurable(store.MainPath, "{ this is not json");

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.RecoveredFromBackup, r.Status);
        Assert.Equal(1, r.Config.Zones[0].X);
        var corrupt = box.Fs.EnumerateFiles(store.DirectoryPath, "config.corrupt-*.json").ToList();
        Assert.Single(corrupt);
        Assert.Equal("{ this is not json", box.Fs.ReadAllText(corrupt[0]));
        Assert.False(box.Fs.FileExists(store.MainPath));
    }

    [Fact]
    public void AfterRecovery_NextSave_WritesFreshMain_AndNeverCorruptsBak()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        store.Save(OneZone(x: 1));
        store.Save(OneZone(x: 2));
        box.Fs.WriteAllTextDurable(store.MainPath, "garbage");
        var r = store.Load();

        Assert.True(store.Save(r.Config));

        var main = ConfigSerializer.Deserialize(box.Fs.ReadAllText(store.MainPath));
        Assert.Equal(1, main.Zones[0].X);
        Assert.Equal(ConfigLoadStatus.Loaded, NewStore(box).Load().Status);
    }

    [Fact]
    public void CorruptMainAndBackup_FallBackToDefaults_ButKeepCorruptFiles()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        box.Fs.WriteAllTextDurable(store.MainPath, "");
        box.Fs.WriteAllTextDurable(store.BackupPath, "[]");

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.CorruptReplacedWithDefaults, r.Status);
        Assert.Empty(r.Config.Zones);
        Assert.Single(box.Fs.EnumerateFiles(store.DirectoryPath, "config.corrupt-*.json"));
        Assert.True(box.Fs.FileExists(store.BackupPath));
    }

    [Fact]
    public void MissingMain_WithValidLeftoverTemp_RecoversTemp_CrashDuringFirstSave()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        box.Fs.WriteAllTextDurable(store.TempPath, ConfigSerializer.Serialize(OneZone(x: 77)));

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.RecoveredFromTemp, r.Status);
        Assert.Equal(77, r.Config.Zones[0].X);
    }

    [Fact]
    public void LeftoverPartialTemp_IsIgnored_WhenMainIsFine()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        store.Save(OneZone(x: 5));
        box.Fs.WriteAllTextDurable(store.TempPath, "{ \"schemaVersion\": 1, \"zones\": [ { \"x\": ");

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.Loaded, r.Status);
        Assert.Equal(5, r.Config.Zones[0].X);
    }

    [Fact]
    public void PartialTemp_AndNoMain_IsIgnored_AndDefaultsCreated()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        box.Fs.WriteAllTextDurable(store.TempPath, "{ \"schemaVersion\": 1, ");

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.Created, r.Status);
    }

    [Fact]
    public void NewerSchema_IsReadOnly_AndNeverOverwritten()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        const string future = """{ "schemaVersion": 42, "somethingNew": true }""";
        box.Fs.WriteAllTextDurable(store.MainPath, future);

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.ReadOnlyNewerVersion, r.Status);
        Assert.True(r.IsReadOnly);
        Assert.False(store.Save(OneZone()));
        Assert.Equal(future, box.Fs.ReadAllText(store.MainPath));
        Assert.Empty(box.Fs.EnumerateFiles(store.DirectoryPath, "config.corrupt-*.json"));
    }

    [Fact]
    public void Utf8Bom_AtStart_IsAccepted()
    {
        using var box = new TempSandbox();
        var store = NewStore(box);
        store.Load();
        var withBom = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble()) + ConfigSerializer.Serialize(OneZone());
        File.WriteAllBytes(store.MainPath, Encoding.UTF8.GetBytes(withBom));

        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.Loaded, r.Status);
    }

    [Fact]
    public void PolishAndGermanDirectoryNames_Work()
    {
        using var box = new TempSandbox("Użytkownik Müller — Größe łódź ąęśćżźń");
        var store = NewStore(box);
        store.Load();

        Assert.True(store.Save(OneZone("Zażółć 🦈 Größe")));

        Assert.Equal("Zażółć 🦈 Größe", NewStore(box).Load().Config.Zones[0].Title);
    }

    [Fact]
    public void VeryLongPaths_Work()
    {
        var deep = string.Join(Path.DirectorySeparatorChar, Enumerable.Repeat(new string('d', 40), 9)); // > 360 chars
        using var box = new TempSandbox(deep);
        var store = NewStore(box);
        store.Load();

        Assert.True(store.MainPath.Length > 300);
        Assert.True(store.Save(OneZone()));
        Assert.Equal(ConfigLoadStatus.Loaded, NewStore(box).Load().Status);
    }

    [Fact]
    public void UnreadableMainThatCannotBeQuarantined_GoesReadOnly_ToProtectBackup()
    {
        using var box = new TempSandbox();
        var faulty = new FaultInjectingFileSystem(box.Fs);
        var store = NewStore(box, faulty);
        store.Load();
        store.Save(OneZone(x: 1));
        store.Save(OneZone(x: 2));
        box.Fs.WriteAllTextDurable(store.MainPath, "garbage");

        faulty.FailOn(nameof(IFileSystem.MoveFile)); // quarantine fails
        var r = store.Load();

        Assert.Equal(ConfigLoadStatus.RecoveredFromBackup, r.Status);
        Assert.False(store.Save(r.Config)); // read-only: does not rotate garbage into .bak
        Assert.Equal(1, ConfigSerializer.Deserialize(box.Fs.ReadAllText(store.BackupPath)).Zones[0].X);
    }
}
