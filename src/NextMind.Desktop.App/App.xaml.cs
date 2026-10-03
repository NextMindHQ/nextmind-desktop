using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using NextMind.Desktop.Core;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Core.Managed;
using NextMind.Desktop.Core.Startup;
using NextMind.Desktop.Shell;

namespace NextMind.Desktop.App;

public partial class App : Application
{
    private const int MenuOpen = 1;
    private const int MenuShowHide = 2;
    private const int MenuCreateZone = 3;
    private const int MenuAutostart = 4;
    private const int MenuSettings = 5;
    private const int MenuExit = 6;

    private SingleInstance? _instance;
    private MessageWindow? _messageWindow;
    private TrayIcon? _tray;
    private ZoneManager? _zones;
    private ControlCenterWindow? _controlCenter;
    private IconService? _icons;
    private AppConfig? _config;
    private StartupManager? _startup;
    private string? _exePath;
    private DispatcherTimer? _trayRetryTimer;
    private int _trayRetryAttempt;
    private ILog _log = NullLog.Instance;
    private bool _exiting;

    /// <summary>Set when config is redirected for development/smoke tests: never touch the real autostart entry then.</summary>
    private static bool IsDevelopmentRun
        => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(AppPaths.ConfigDirectoryOverrideVariable));

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instance = SingleInstance.TryAcquire();
        if (_instance is null)
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        _log = new FileLog(AppPaths.LogDirectory());
        _log.Info("---- NextMind Desktop starting ----");
        DispatcherUnhandledException += (_, args) =>
        {
            _log.Error("Unhandled UI exception (continuing).", args.Exception);
            args.Handled = true;
        };

        try
        {
            Start();
        }
        catch (Exception ex)
        {
            _log.Error("Startup failed.", ex);
            Cleanup();
            Shutdown(1);
        }
    }

    private void Start()
    {
        var configDir = AppPaths.ConfigDirectory();
        var fs = new GuardedFileSystem(new RealFileSystem(), configDir, ProtectedPaths.FromEnvironment());
        var store = new ConfigStore(fs, configDir, _log);

        var loaded = store.Load();
        _config = loaded.Config;
        _log.Info($"Config: {loaded.Status}{(loaded.Detail is null ? string.Empty : " - " + loaded.Detail)}; zones={_config.Zones.Count}");
        _log.Info($"Desktop known folder: {KnownFolders.TryGetPath(KnownFolders.Desktop)}");

        var layer = new DesktopLayer();
        _icons = new IconService(Dispatcher, _log);

        // Managed Desktop items: a separate, tightly scoped filesystem (managed storage + journal + the user's Desktop, nothing else).
        var desktops = CreateDesktopFolders(out var fakeDesktop);
        var managedPaths = ManagedPaths.ForConfigDirectory(configDir);
        var managedFs = new ScopedFileSystem(new RealFileSystem(), [managedPaths.Root, managedPaths.JournalDirectory, desktops.UserDesktop]);
        var service = new ManagedItemService(managedFs, managedPaths, desktops, new JournalStore(managedFs, managedPaths.JournalDirectory, _log), _log);
        var assessor = new MoveAssessor(managedFs, managedPaths.Root);

        // SAFETY GATE. Off by default: Desktop items are then only referenced, never moved. The environment override works ONLY with a fake desktop.
        var envEnable = Environment.GetEnvironmentVariable("NEXTMIND_ENABLE_MANAGED_MOVES") == "1";
        bool Gate() => _config!.ManagedDesktopMovesEnabled || (fakeDesktop && envEnable);
        _log.Info($"Managed Desktop Moves: {(Gate() ? "ENABLED" : "DISABLED")}{(fakeDesktop ? " (development: FAKE Desktop " + desktops.UserDesktop + ")" : string.Empty)}.");

        _zones = new ZoneManager(_config, store, layer, _log, Dispatcher, _icons, service, assessor, desktops, Gate);

        var startedAt = Process.GetCurrentProcess().StartTime;
        _zones.FirstZoneRendered += () =>
            _log.Info($"Cold start: first zone rendered {(DateTime.Now - startedAt).TotalMilliseconds:F0} ms after process start. {layer.Describe()}");

        _messageWindow = new MessageWindow();
        _messageWindow.HandlerFailed += ex => _log.Error("Message handler failed.", ex);
        _messageWindow.TaskbarCreated += OnTaskbarCreated;
        _messageWindow.DisplayChanged += () => _zones!.ClampAll();
        _messageWindow.ActivateRequested += () => _zones!.ShowAll();
        _messageWindow.TrayMouse += OnTrayMouse;

        _tray = new TrayIcon(_messageWindow, "NextMind Desktop", Environment.ProcessPath);
        EnsureTray();

        // Finish or roll back anything a crash left half-done (decided only from what is on disk), BEFORE the zones show their items.
        var recovery = _zones.RecoverInterruptedOperations();

        if (_config.Zones.Count == 0 && !loaded.IsReadOnly)
        {
            _zones.CreateZone(ZoneFactory.FirstZoneTitle); // first run: the default "🦈 NextMind" zone
        }
        else
        {
            _zones.OpenConfiguredZones();
        }

        ConfigureAutostart(loaded.IsReadOnly);

        var attention = recovery.Where(a => a.Outcome == RecoveryOutcome.NeedsAttention).Select(a => a.Message).ToList();
        if (attention.Count > 0)
        {
            ChoiceWindow.Inform("NextMind Desktop needs your attention", string.Join("\n\n", attention));
        }

        SessionEnding += (_, _) => _zones?.Flush();
    }

    /// <summary>
    /// The Desktop folders from the Known Folder API. Development only: with a redirected config AND NEXTMIND_FAKE_USER_DESKTOP set, a throw-away folder
    /// plays the Desktop — and only if it is not inside any real user folder. This is how the managed-move UI is exercised without touching the real Desktop.
    /// </summary>
    private IDesktopFolders CreateDesktopFolders(out bool fake)
    {
        fake = false;
        var fakePath = Environment.GetEnvironmentVariable("NEXTMIND_FAKE_USER_DESKTOP");
        if (!string.IsNullOrEmpty(fakePath) && IsDevelopmentRun)
        {
            var full = Path.GetFullPath(fakePath);
            if (ProtectedPaths.FromEnvironment().Any(p => PathUtil.IsSameOrUnder(full, p)))
            {
                _log.Error($"Refusing NEXTMIND_FAKE_USER_DESKTOP '{full}': it is inside a real user folder. Using the real Known Folder Desktop (managed moves stay gated).");
            }
            else
            {
                fake = true;
                return new StaticDesktopFolders(full, null);
            }
        }

        return new KnownFolderDesktops();
    }

    // ---------- autostart ("Start with Windows") ----------

    private void ConfigureAutostart(bool configReadOnly)
    {
        _exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(_exePath) || string.Equals(Path.GetFileName(_exePath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            _log.Info("Autostart unavailable: not running from the app's own executable.");
            return;
        }

        _startup = new StartupManager(new RegistryStartupStore(), new RealPathProbe());

        if (IsDevelopmentRun)
        {
            _log.Info("Development run (config redirected): autostart is not touched automatically.");
            return;
        }

        try
        {
            if (!_config!.AutostartDecided)
            {
                // First run (or first run after upgrading from a build without autostart): default is ON.
                var wrote = _startup.Enable(_exePath);
                _config.AutostartDecided = true;
                _config.AutostartEnabled = true;
                _log.Info($"Autostart default applied: enabled ({(wrote ? "entry written" : "entry already correct")}).");
                if (!configReadOnly)
                {
                    _zones?.Flush();
                }
            }
            else if (configReadOnly)
            {
                // A quarantined/defaulted config says nothing reliable about the user's wish: only repair an entry that exists.
                if (_startup.Reconcile(_exePath))
                {
                    _log.Info("Autostart entry was stale or broken and now points at this executable.");
                }
            }
            else
            {
                // The config is the source of truth: make the Run entry match it (repair when ON, remove a leftover when OFF).
                var result = _startup.Sync(_exePath, _config.AutostartEnabled);
                _log.Info($"Autostart preference is {(_config.AutostartEnabled ? "ON" : "OFF")}: entry {result.ToString().ToLowerInvariant()}.");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Could not configure autostart (the app keeps running; use the tray checkbox to retry).", ex);
        }
    }

    private bool AutostartChecked()
    {
        if (_startup is null || _exePath is null)
        {
            return false;
        }

        try
        {
            return _startup.GetState(_exePath) != StartupState.Disabled;
        }
        catch (Exception ex)
        {
            _log.Error("Could not read the autostart state.", ex);
            return false;
        }
    }

    private void ToggleAutostart()
    {
        if (_startup is null || _exePath is null)
        {
            return;
        }

        var turnOn = true;
        try
        {
            turnOn = _startup.GetState(_exePath) == StartupState.Disabled;
            if (turnOn)
            {
                _startup.Enable(_exePath);
                _log.Info("Start with Windows: ON (from the tray).");
            }
            else
            {
                _startup.Disable();
                _log.Info("Start with Windows: OFF (from the tray).");
            }
        }
        catch (Exception ex)
        {
            _log.Error("Changing 'Start with Windows' failed.", ex);
            return;
        }

        // The user has now made an explicit choice: it is stored as the preference (source of truth) and the first-run default never runs again.
        if (_config is not null)
        {
            _config.AutostartDecided = true;
            _config.AutostartEnabled = turnOn;
            _zones?.Flush();
        }
    }

    // ---------- tray ----------

    /// <summary>Adds the tray icon; if the taskbar is not there yet (very early after logon) retries with a bounded backoff.</summary>
    private void EnsureTray()
    {
        _trayRetryTimer?.Stop();

        if (_tray!.Add())
        {
            _log.Info("Tray icon added.");
            return;
        }

        var delay = BackoffSchedule.Next(_trayRetryAttempt++);
        if (delay is null)
        {
            _log.Warn("Tray icon could not be added; it will be added when Explorer announces the taskbar (TaskbarCreated).");
            return;
        }

        _log.Warn($"Tray icon could not be added yet; retrying in {delay.Value.TotalSeconds:0.#} s.");
        _trayRetryTimer ??= new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
        _trayRetryTimer.Interval = delay.Value;
        _trayRetryTimer.Tick -= OnTrayRetryTick;
        _trayRetryTimer.Tick += OnTrayRetryTick;
        _trayRetryTimer.Start();
    }

    private void OnTrayRetryTick(object? sender, EventArgs e) => EnsureTray();

    private void OnTaskbarCreated()
    {
        _log.Info("Explorer restarted (TaskbarCreated): re-adding tray icon and re-anchoring zones.");
        _trayRetryAttempt = 0;
        EnsureTray();
        _zones?.RepinAll();
    }

    private void OnTrayMouse(TrayMouseEvent e)
    {
        switch (e)
        {
            case TrayMouseEvent.DoubleClick:
                OpenControlCenter();
                break;

            case TrayMouseEvent.RightClick:
                ShowTrayMenu();
                break;
        }
    }

    private void ShowTrayMenu()
    {
        if (_tray is null || _zones is null)
        {
            return;
        }

        var choice = _tray.ShowMenu(
        [
            new TrayMenuItem(MenuOpen, "Open NextMind Desktop", IsDefault: true),
            TrayMenuItem.Separator,
            new TrayMenuItem(MenuShowHide, "Show / Hide Zones"),
            new TrayMenuItem(MenuCreateZone, "Create Zone"),
            TrayMenuItem.Separator,
            new TrayMenuItem(MenuAutostart, "Start with Windows", Enabled: _startup is not null, Checked: AutostartChecked()),
            TrayMenuItem.Separator,
            new TrayMenuItem(MenuSettings, "Settings", Enabled: false),
            new TrayMenuItem(MenuExit, "Exit"),
        ]);

        switch (choice)
        {
            case MenuOpen:
                OpenControlCenter();
                break;
            case MenuShowHide:
                _zones.ToggleShowHide();
                break;
            case MenuCreateZone:
                PromptAndCreateZone();
                break;
            case MenuAutostart:
                ToggleAutostart();
                break;
            case MenuExit:
                ExitApp();
                break;
        }
    }

    private void PromptAndCreateZone()
    {
        var name = PromptWindow.AskText("Create Zone", "Zone name:", string.Empty, "Create");
        if (name is not null)
        {
            _zones?.CreateZone(name);
        }
    }

    /// <summary>Opens (or brings forward) the Control Center. It is a normal window, opened on demand and never kept around.</summary>
    private void OpenControlCenter()
    {
        if (_zones is null)
        {
            return;
        }

        if (_controlCenter is { IsLoaded: true })
        {
            _controlCenter.Refresh();
            _controlCenter.Activate();
            return;
        }

        _controlCenter = new ControlCenterWindow(
            _zones,
            AutostartChecked,
            () => _startup is not null,
            ToggleAutostart,
            PromptAndCreateZone,
            ExitApp);
        _controlCenter.Closed += (_, _) => _controlCenter = null;
        _controlCenter.Show();
        _controlCenter.Activate();
    }

    /// <summary>Exit closes the app NOW. It does not change "Start with Windows".</summary>
    private void ExitApp()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        _log.Info("Exit requested.");
        _controlCenter?.Close();
        _zones?.Flush();
        Cleanup();
        Shutdown();
    }

    private void Cleanup()
    {
        _trayRetryTimer?.Stop();
        _tray?.Dispose();
        _tray = null;
        _icons?.Dispose();
        _icons = null;
        _messageWindow?.Dispose();
        _messageWindow = null;
        _instance?.Dispose();
        _instance = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Cleanup();
        base.OnExit(e);
    }
}
