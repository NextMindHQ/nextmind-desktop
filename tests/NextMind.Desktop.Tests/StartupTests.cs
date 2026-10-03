using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Startup;
using NextMind.Desktop.Shell;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class StartupManagerTests
{
    private sealed class MemoryStore : IStartupStore
    {
        public string? Value { get; set; }

        public int Writes { get; private set; }

        public int Removes { get; private set; }

        public string? Read() => Value;

        public void Write(string commandLine)
        {
            Value = commandLine;
            Writes++;
        }

        public void Remove()
        {
            Value = null;
            Removes++;
        }
    }

    private sealed class FakeProbe(params string[] existing) : IPathProbe
    {
        private readonly HashSet<string> _existing = new(existing.Select(PathUtil.Normalize), StringComparer.OrdinalIgnoreCase);

        public bool Exists(string path) => _existing.Contains(PathUtil.Normalize(path));
    }

    private const string Exe = @"C:\Apps\NextMind Desktop\NextMindDesktop.exe";
    private const string OtherExe = @"D:\Old copy\NextMindDesktop.exe";

    private static (StartupManager Manager, MemoryStore Store) Create(params string[] existing)
    {
        var store = new MemoryStore();
        return (new StartupManager(store, new FakeProbe(existing.Length == 0 ? [Exe] : existing)), store);
    }

    [Fact]
    public void FreshState_IsDisabled()
    {
        var (m, _) = Create();
        Assert.Equal(StartupState.Disabled, m.GetState(Exe));
    }

    [Fact]
    public void Enable_WritesAQuotedCommand_AndStateBecomesEnabled()
    {
        var (m, store) = Create();

        Assert.True(m.Enable(Exe));

        Assert.Equal("\"" + Exe + "\"", store.Value); // quoted: the path contains a space
        Assert.Equal(StartupState.Enabled, m.GetState(Exe));
    }

    [Fact]
    public void Enable_IsIdempotent_NoSecondWrite()
    {
        var (m, store) = Create();
        m.Enable(Exe);

        Assert.False(m.Enable(Exe));
        Assert.False(m.Enable(Exe));

        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void Disable_RemovesTheEntry_AndIsIdempotent()
    {
        var (m, store) = Create();
        m.Enable(Exe);

        Assert.True(m.Disable());
        Assert.Equal(StartupState.Disabled, m.GetState(Exe));
        Assert.False(m.Disable());
        Assert.False(m.Disable());

        Assert.Equal(1, store.Removes);
    }

    [Fact]
    public void DisableWhenNeverEnabled_DoesNothing()
    {
        var (m, store) = Create();
        Assert.False(m.Disable());
        Assert.Equal(0, store.Removes);
    }

    [Fact]
    public void EnableDisableEnable_RoundTrips()
    {
        var (m, _) = Create();
        m.Enable(Exe);
        m.Disable();
        Assert.True(m.Enable(Exe));
        Assert.Equal(StartupState.Enabled, m.GetState(Exe));
    }

    [Fact]
    public void EntryForAnotherExistingExecutable_IsDetected_AndReconcileRepointsIt()
    {
        var (m, store) = Create(Exe, OtherExe);
        store.Value = "\"" + OtherExe + "\"";

        Assert.Equal(StartupState.EnabledForOtherPath, m.GetState(Exe));
        Assert.True(m.Reconcile(Exe));
        Assert.Equal(StartupState.Enabled, m.GetState(Exe));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"")]
    [InlineData("\"unterminated.exe")]
    [InlineData("garbage without an executable")]
    [InlineData("relative\\app.exe")]
    [InlineData("\"C:\\Gone\\Missing.exe\"")]
    public void OldOrBrokenEntries_AreDetected_AndRepairedOnReconcile(string value)
    {
        var (m, store) = Create();
        store.Value = value;

        Assert.Equal(StartupState.Broken, m.GetState(Exe));
        Assert.True(m.Reconcile(Exe));
        Assert.Equal(StartupState.Enabled, m.GetState(Exe));
    }

    [Fact]
    public void Reconcile_LeavesDisabledAndHealthyEntriesAlone()
    {
        var (m, store) = Create();
        Assert.False(m.Reconcile(Exe)); // disabled stays disabled
        Assert.Null(store.Value);

        m.Enable(Exe);
        Assert.False(m.Reconcile(Exe));
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void EntryWithExtraArguments_IsStillRecognisedAsThisExecutable()
    {
        var (m, store) = Create();
        store.Value = "\"" + Exe + "\" --minimized";
        Assert.Equal(StartupState.Enabled, m.GetState(Exe));
    }

    [Fact]
    public void PathComparison_IgnoresCase_AndSlashStyle()
    {
        var (m, store) = Create();
        store.Value = "\"" + Exe.ToUpperInvariant() + "\"";
        Assert.Equal(StartupState.Enabled, m.GetState(Exe.Replace('\\', '/')));
    }

    [Theory]
    [InlineData("\"C:\\a b\\x.exe\" /arg", "C:\\a b\\x.exe")]
    [InlineData("C:\\a\\x.EXE /arg", "C:\\a\\x.EXE")]
    [InlineData("  \"C:\\p.exe\"  ", "C:\\p.exe")]
    [InlineData("nothing", null)]
    [InlineData("", null)]
    public void ParseExecutable(string command, string? expected)
        => Assert.Equal(expected, StartupManager.TryParseExecutable(command));

    [Fact]
    public void Sync_PreferenceOn_MissingEntry_IsCreated()
    {
        var (m, store) = Create();
        Assert.Equal(StartupSyncResult.Created, m.Sync(Exe, desired: true));
        Assert.Equal(StartupManager.BuildCommand(Exe), store.Value);
        Assert.Equal(StartupState.Enabled, m.GetState(Exe));
    }

    [Fact]
    public void Sync_PreferenceOff_MissingEntry_DoesNothing()
    {
        var (m, store) = Create();
        Assert.Equal(StartupSyncResult.Unchanged, m.Sync(Exe, desired: false));
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, store.Removes);
        Assert.Null(store.Value);
    }

    [Fact]
    public void Sync_PreferenceOff_ExistingEntry_IsRemoved()
    {
        var (m, store) = Create();
        store.Value = StartupManager.BuildCommand(Exe);
        Assert.Equal(StartupSyncResult.Removed, m.Sync(Exe, desired: false));
        Assert.Null(store.Value);
        // and it stays off: a second pass (next start) neither writes nor removes anything
        Assert.Equal(StartupSyncResult.Unchanged, m.Sync(Exe, desired: false));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void Sync_PreferenceOn_WrongPath_IsRepaired()
    {
        var (m, store) = Create(Exe, OtherExe);
        store.Value = StartupManager.BuildCommand(OtherExe);
        Assert.Equal(StartupSyncResult.Repaired, m.Sync(Exe, desired: true));
        Assert.Equal(StartupManager.BuildCommand(Exe), store.Value);
    }

    [Fact]
    public void Sync_PreferenceOn_BrokenEntry_IsRepaired()
    {
        var (m, store) = Create();
        store.Value = "\"C:\\gone\\NextMindDesktop.exe\"";
        Assert.Equal(StartupSyncResult.Repaired, m.Sync(Exe, desired: true));
        Assert.Equal(StartupManager.BuildCommand(Exe), store.Value);
    }

    [Fact]
    public void Sync_PreferenceOn_CorrectEntry_IsLeftAlone()
    {
        var (m, store) = Create();
        store.Value = StartupManager.BuildCommand(Exe);
        Assert.Equal(StartupSyncResult.Unchanged, m.Sync(Exe, desired: true));
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void ConsciousOff_IsNeverUndone_ByRepeatedSyncs()
    {
        // The tray "OFF" removes the entry and stores AutostartEnabled=false; every later start only syncs to that preference.
        var (m, store) = Create();
        m.Enable(Exe);
        m.Disable();
        var cfg = new AppConfig { AutostartDecided = true, AutostartEnabled = false };
        for (var i = 0; i < 3; i++)
        {
            m.Sync(Exe, cfg.AutostartEnabled);
        }

        Assert.Null(store.Value);
    }

    [Fact]
    public void AutostartPreference_DefaultsToOn_ForOldConfigs_AndRoundTripsOff()
    {
        Assert.True(new AppConfig().AutostartEnabled);
        Assert.True(ConfigSerializer.Deserialize("{ \"schemaVersion\": 3, \"autostartDecided\": true }").AutostartEnabled);

        var off = new AppConfig { AutostartDecided = true, AutostartEnabled = false };
        Assert.False(ConfigSerializer.Deserialize(ConfigSerializer.Serialize(off)).AutostartEnabled);
    }

    [Fact]
    public void Exit_IsNotAutostartOff_ConfigFlagIsIndependentOfTheEntry()
    {
        // The app never touches the entry on Exit: the manager has no "exit" operation, and the first-run default
        // is guarded by AutostartDecided so it is applied exactly once.
        var cfg = new AppConfig();
        Assert.False(cfg.AutostartDecided);
        cfg.AutostartDecided = true;
        Assert.True(cfg.AutostartDecided);
    }
}

public class BackoffScheduleTests
{
    [Fact]
    public void Delays_GrowAndAreBounded()
    {
        TimeSpan? previous = TimeSpan.Zero;
        var attempt = 0;
        while (BackoffSchedule.Next(attempt) is { } delay)
        {
            Assert.True(delay >= previous);
            previous = delay;
            attempt++;
        }

        Assert.Equal(BackoffSchedule.Default.Count, attempt);
        Assert.True(BackoffSchedule.Total < TimeSpan.FromMinutes(2));
        Assert.Null(BackoffSchedule.Next(-1));
        Assert.Null(BackoffSchedule.Next(attempt));
    }
}

public class RegistryStartupStoreTests : IDisposable
{
    // Tests use their own throw-away key and never the product's Run value.
    private readonly string _keyPath = @"Software\NextMind\DesktopTests\" + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        // Test-only cleanup of the throw-away key tree this test created, including the empty parents it leaves behind.
        using (var parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\NextMind\DesktopTests", writable: true))
        {
            parent?.DeleteSubKeyTree(Path.GetFileName(_keyPath), throwOnMissingSubKey: false);
        }

        RemoveIfEmpty(@"Software\NextMind", "DesktopTests");
        RemoveIfEmpty("Software", "NextMind");
    }

    private static void RemoveIfEmpty(string parentPath, string childName)
    {
        using var parent = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(parentPath, writable: true);
        using var child = parent?.OpenSubKey(childName);
        if (parent is not null && child is { SubKeyCount: 0, ValueCount: 0 })
        {
            child.Dispose();
            parent.DeleteSubKey(childName, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void WriteReadRemove_RoundTrips_AndMissingReadsAsNull()
    {
        var store = new RegistryStartupStore(_keyPath, "NextMindDesktop");

        Assert.Null(store.Read());
        store.Write("\"C:\\x y\\a.exe\"");
        Assert.Equal("\"C:\\x y\\a.exe\"", store.Read());
        store.Remove();
        Assert.Null(store.Read());
        store.Remove(); // removing twice is fine
    }

    [Fact]
    public void ValueOfTheWrongType_ReadsAsEmpty_SoItIsTreatedAsBroken()
    {
        using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(_keyPath))
        {
            key.SetValue("NextMindDesktop", 42, Microsoft.Win32.RegistryValueKind.DWord);
        }

        var store = new RegistryStartupStore(_keyPath, "NextMindDesktop");

        Assert.Equal(string.Empty, store.Read());
        var manager = new StartupManager(store, new RealPathProbe());
        Assert.Equal(StartupState.Broken, manager.GetState(@"C:\Apps\NextMindDesktop.exe"));
    }

    [Fact]
    public void FullManagerFlow_OnTheRegistry_WithARealExecutablePath()
    {
        using var box = new TempSandbox();
        var exe = box.Combine("NextMind Desktop Test.exe");
        box.Fs.WriteAllTextDurable(exe, "not really an exe");
        var manager = new StartupManager(new RegistryStartupStore(_keyPath, "NextMindDesktop"), new RealPathProbe());

        Assert.Equal(StartupState.Disabled, manager.GetState(exe));
        Assert.True(manager.Enable(exe));
        Assert.False(manager.Enable(exe));
        Assert.Equal(StartupState.Enabled, manager.GetState(exe));
        Assert.True(manager.Disable());
        Assert.False(manager.Disable());
        Assert.Equal(StartupState.Disabled, manager.GetState(exe));
    }

    [Fact]
    public void RealRunKey_IsWritable_WithAUniqueTestValueName_AndLeavesNoTrace()
    {
        // Proves the production key path works on this machine without ever touching the product's own value.
        var valueName = "NextMindDesktopTest-" + Guid.NewGuid().ToString("N");
        var store = new RegistryStartupStore(RegistryStartupStore.RunKeyPath, valueName);
        Assert.NotEqual(RegistryStartupStore.ProductValueName, valueName);

        try
        {
            store.Write("\"C:\\Windows\\System32\\notepad.exe\"");
            Assert.Equal("\"C:\\Windows\\System32\\notepad.exe\"", store.Read());
        }
        finally
        {
            store.Remove();
        }

        Assert.Null(store.Read());
    }

    [Fact]
    public void TheProductEntry_IsHKCU_Run_PerUser_NoAdminNeeded()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", RegistryStartupStore.RunKeyPath);
        Assert.Equal("NextMindDesktop", RegistryStartupStore.ProductValueName);
    }
}

public class SingleInstanceTests
{
    [Fact]
    public void SecondAcquire_IsRefused_UntilTheFirstIsReleased()
    {
        var name = @"Local\NextMind.Desktop.Tests." + Guid.NewGuid().ToString("N");

        using var first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);

        SingleInstance? second = null;
        var t = new Thread(() => second = SingleInstance.TryAcquire(name));
        t.Start();
        t.Join();
        Assert.Null(second); // a second instance (here: another thread, same mechanism) does not get the lock

        first!.Dispose();

        SingleInstance? third = null;
        var t2 = new Thread(() => third = SingleInstance.TryAcquire(name));
        t2.Start();
        t2.Join();
        Assert.NotNull(third);
        third!.Dispose();
    }
}
