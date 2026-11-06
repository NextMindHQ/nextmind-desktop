using System.Windows.Threading;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Core.Widgets;
using NextMind.Desktop.Shell;

namespace NextMind.Desktop.App;

public sealed class WidgetManager : IDisposable
{
    private readonly AppConfig _config;
    private readonly DesktopLayer _layer;
    private readonly ILog _log;
    private readonly Action _save;
    private readonly DispatcherTimer _clockTimer;
    private readonly List<WidgetWindow> _windows = [];

    public WidgetManager(AppConfig config, DesktopLayer layer, ILog log, Dispatcher dispatcher, Action save)
    {
        _config = config;
        _layer = layer;
        _log = log;
        _save = save;
        _clockTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _clockTimer.Tick += ClockTick;
    }

    public IReadOnlyList<WidgetConfig> Widgets => _config.Widgets;

    public event Action? Changed;

    public void OpenConfiguredWidgets()
    {
        foreach (var widget in _config.Widgets.ToList())
        {
            if (!widget.Hidden)
            {
                Open(widget);
            }
        }

        UpdateClocksAndRearm();
    }

    public void Add(WidgetKind kind)
    {
        var widget = WidgetCatalog.Create(kind, _config.Widgets, Monitors.GetPrimaryWorkArea(), Monitors.GetSystemDpi());
        _config.Widgets.Add(widget);
        Open(widget);
        UpdateClocksAndRearm();
        _save();
        Changed?.Invoke();
    }

    public void ReloadProfile()
    {
        CloseAll();
        OpenConfiguredWidgets();
        Changed?.Invoke();
    }

    public void Show(string widgetId)
    {
        var widget = _config.Widgets.FirstOrDefault(candidate => string.Equals(candidate.Id, widgetId, StringComparison.OrdinalIgnoreCase));
        if (widget is null)
        {
            return;
        }

        widget.Hidden = false;
        if (_windows.All(window => !string.Equals(window.Config.Id, widget.Id, StringComparison.OrdinalIgnoreCase)))
        {
            Open(widget);
        }

        UpdateClocksAndRearm();
        _save();
        Changed?.Invoke();
    }

    public void ClampAll()
    {
        var areas = Monitors.GetWorkAreas();
        foreach (var window in _windows)
        {
            window.ClampToWorkAreas(areas);
        }

        _save();
    }

    public void RepinAll()
    {
        foreach (var window in _windows)
        {
            window.Repin();
        }
    }

    private void Open(WidgetConfig widget)
    {
        var window = new WidgetWindow(widget, _layer, _log);
        window.Changed += OnWindowChanged;
        window.RemoveRequested += Remove;
        window.EditNoteRequested += EditNote;
        _windows.Add(window);
        window.Show();
        if (widget.Kind == WidgetKind.Clock)
        {
            window.UpdateClock(DateTime.Now);
        }
    }

    private void OnWindowChanged(WidgetWindow window)
    {
        window.Snapshot();
        _save();
        Changed?.Invoke();
    }

    private void Remove(WidgetWindow window)
    {
        window.Snapshot();
        window.Config.Hidden = true;
        _windows.Remove(window);
        window.Close();
        UpdateClocksAndRearm();
        _save();
        Changed?.Invoke();
    }

    private void EditNote(WidgetWindow window)
    {
        var text = NoteEditorWindow.Ask(window.Config.NoteText);
        if (text is null)
        {
            return;
        }

        window.SetNote(text);
        _save();
        Changed?.Invoke();
    }

    private void ClockTick(object? sender, EventArgs e)
    {
        _clockTimer.Stop();
        UpdateClocksAndRearm();
    }

    private void UpdateClocksAndRearm()
    {
        _clockTimer.Stop();
        var clocks = _windows.Where(window => window.Config.Kind == WidgetKind.Clock).ToList();
        if (clocks.Count == 0)
        {
            return;
        }

        var now = DateTime.Now;
        foreach (var clock in clocks)
        {
            clock.UpdateClock(now);
        }

        var nextMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind).AddMinutes(1);
        _clockTimer.Interval = nextMinute - now + TimeSpan.FromMilliseconds(50);
        _clockTimer.Start();
    }

    private void CloseAll()
    {
        _clockTimer.Stop();
        foreach (var window in _windows.ToList())
        {
            window.Close();
        }

        _windows.Clear();
    }

    public void Dispose()
    {
        CloseAll();
        _clockTimer.Tick -= ClockTick;
    }
}
