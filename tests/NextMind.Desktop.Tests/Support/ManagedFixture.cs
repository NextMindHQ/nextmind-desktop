using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Managed;

namespace NextMind.Desktop.Tests.Support;

/// <summary>
/// TEMP ROOT + FAKE DESKTOP + FAKE MANAGED STORAGE. Nothing here can reach the real Desktop: the roots are checked by
/// <see cref="RealDesktopGuard"/> and every filesystem call goes through a <see cref="GuardedFileSystem"/> that denies the real user folders.
/// </summary>
public sealed class ManagedFixture : IDisposable
{
    private int _ids;

    public ManagedFixture(string? leaf = null)
    {
        Box = new TempSandbox(leaf);
        Desktop = Box.Combine("Desktop");
        PublicDesktop = Box.Combine("Public Desktop");
        var config = Box.Combine("Config");

        foreach (var root in new[] { Desktop, PublicDesktop, config })
        {
            RealDesktopGuard.AssertFake(root);
        }

        Box.Fs.CreateDirectory(Desktop);
        Box.Fs.CreateDirectory(PublicDesktop);

        Faulty = new FaultInjectingFileSystem(Box.Fs);
        Paths = ManagedPaths.ForConfigDirectory(config);
        Desktops = new StaticDesktopFolders(Desktop, PublicDesktop);
        Journal = new JournalStore(Faulty, Paths.JournalDirectory);
        Service = new ManagedItemService(Faulty, Paths, Desktops, Journal, clock: () => Now, newId: () => $"item-{++_ids:D3}");

        Config = new AppConfig();
        Zone = ZoneCatalog.AddZone(Config, "🎮 Gry", new RectPx(0, 0, 1920, 1040), 96);
    }

    public TempSandbox Box { get; }

    public string Desktop { get; }

    public string PublicDesktop { get; }

    public FaultInjectingFileSystem Faulty { get; }

    public ManagedPaths Paths { get; }

    public IDesktopFolders Desktops { get; }

    public JournalStore Journal { get; }

    public ManagedItemService Service { get; }

    public AppConfig Config { get; }

    public ZoneConfig Zone { get; }

    public DateTime Now { get; set; } = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    public int CommitCalls { get; private set; }

    public bool CommitSucceeds { get; set; } = true;

    /// <summary>Invoked inside every commit, before it reports its result (lets a test look at the world at the commit moment).</summary>
    public Action? OnCommit { get; set; }

    public Func<bool> Commit => () =>
    {
        CommitCalls++;
        OnCommit?.Invoke();
        return CommitSucceeds;
    };

    public string OnDesktop(string name) => Path.Combine(Desktop, name);

    public string CreateDesktopFile(string name, string content = "data")
    {
        var path = OnDesktop(name);
        Box.Fs.WriteAllTextDurable(path, content);
        return path;
    }

    public string CreateDesktopFolder(string name, params (string File, string Content)[] files)
    {
        var path = OnDesktop(name);
        Box.Fs.CreateDirectory(path);
        foreach (var (file, content) in files)
        {
            var target = Path.Combine(path, file);
            Box.Fs.CreateDirectory(Path.GetDirectoryName(target)!);
            Box.Fs.WriteAllTextDurable(target, content);
        }

        return path;
    }

    public string[] PendingJournals() => Box.Fs.DirectoryExists(Paths.JournalDirectory)
        ? Box.Fs.EnumerateFiles(Paths.JournalDirectory, "*.json").ToArray()
        : [];

    public void Dispose() => Box.Dispose();
}
