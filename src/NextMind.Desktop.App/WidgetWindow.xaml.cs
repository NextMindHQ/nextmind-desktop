using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Shell;

namespace NextMind.Desktop.App;

public partial class WidgetWindow : Window
{
    private const double MinWidthDip = 160;
    private const double MinHeightDip = 80;
    private const double ClockMinHeightDip = 110;
    private const double CollapsedHeightDip = 32;
    private const uint WM_WINDOWPOSCHANGING = 0x0046;
    private readonly WidgetConfig _config;
    private readonly DesktopLayer _layer;
    private readonly ILog _log;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private int _expandedHeightPx;

    public WidgetWindow(WidgetConfig config, DesktopLayer layer, ILog log)
    {
        _config = config;
        _layer = layer;
        _log = log;
        InitializeComponent();
        TitleText.Text = config.Kind == WidgetKind.Clock ? "Clock & Date" : "Quick Note";
        ClockPanel.Visibility = config.Kind == WidgetKind.Clock ? Visibility.Visible : Visibility.Collapsed;
        NotePanel.Visibility = config.Kind == WidgetKind.QuickNote ? Visibility.Visible : Visibility.Collapsed;
        NoteText.Text = config.NoteText;
        _expandedHeightPx = config.Height;
        MinWidth = MinWidthDip;
        MinHeight = config.Kind == WidgetKind.Clock ? ClockMinHeightDip : MinHeightDip;
    }

    public WidgetConfig Config => _config;

    public bool IsPinned { get; private set; }

    public event Action<WidgetWindow>? Changed;

    public event Action<WidgetWindow>? RemoveRequested;

    public event Action<WidgetWindow>? EditNoteRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
        DesktopLayer.ApplyDesktopWindowStyles(_hwnd);
        var wanted = new RectPx(_config.X, _config.Y, _config.Width, _config.Height);
        var bounds = ZoneGeometry.ClampToWorkAreas(wanted, Monitors.GetWorkAreas());
        _expandedHeightPx = bounds.Height;
        Monitors.SetWindowBounds(_hwnd, bounds);
        SetCollapsed(_config.Collapsed, applyHeight: _config.Collapsed);
        Repin();
        if (!IsPinned)
        {
            _log.Warn($"Widget '{TitleText.Text}': desktop host not found; using visible fallback z-order.");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(WndProc);
        _source = null;
        base.OnClosed(e);
    }

    public void UpdateClock(DateTime now)
        => ClockPanel.UpdateClock(now);

    public void SetNote(string text)
    {
        _config.NoteText = text;
        NoteText.Text = text;
    }

    public RectPx CurrentBounds()
        => _hwnd == IntPtr.Zero ? new RectPx(_config.X, _config.Y, _config.Width, _config.Height) : Monitors.GetWindowRect(_hwnd);

    public void Snapshot()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var bounds = CurrentBounds();
        _config.X = bounds.X;
        _config.Y = bounds.Y;
        _config.Width = bounds.Width;
        _config.Height = _config.Collapsed ? _expandedHeightPx : bounds.Height;
        _config.Dpi = Monitors.GetDpi(_hwnd);
    }

    public void ClampToWorkAreas(IReadOnlyList<RectPx> workAreas)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var current = CurrentBounds();
        var clamped = ZoneGeometry.ClampToWorkAreas(current, workAreas);
        if (clamped != current)
        {
            Monitors.SetWindowBounds(_hwnd, clamped);
            Snapshot();
        }
    }

    public bool Repin()
    {
        IsPinned = _hwnd != IntPtr.Zero && _layer.Pin(_hwnd);
        return IsPinned;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            _layer.EnforceOnWindowPosChanging(hwnd, lParam);
        }

        return IntPtr.Zero;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            SetCollapsed(!_config.Collapsed);
            Changed?.Invoke(this);
            return;
        }

        if (e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        Changed?.Invoke(this);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e)
    {
        SetCollapsed(!_config.Collapsed);
        Changed?.Invoke(this);
    }

    private void SetCollapsed(bool collapsed, bool applyHeight = true)
    {
        if (!_config.Collapsed && collapsed && _hwnd != IntPtr.Zero)
        {
            _expandedHeightPx = CurrentBounds().Height;
        }

        _config.Collapsed = collapsed;
        MinHeight = collapsed ? CollapsedHeightDip : ExpandedMinHeightDip;
        ContentArea.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        RightGrip.Visibility = BottomGrip.Visibility = CornerGrip.Visibility = CornerMark.Visibility =
            collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseButton.Content = collapsed ? "□" : "—";
        if (applyHeight)
        {
            Height = collapsed ? CollapsedHeightDip : Math.Max(ExpandedMinHeightDip, ZoneGeometry.PxToDip(_expandedHeightPx, _config.Dpi));
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this);

    private void EditNote_Click(object sender, RoutedEventArgs e) => EditNoteRequested?.Invoke(this);

    private void RightGrip_DragDelta(object sender, DragDeltaEventArgs e) => Width = Math.Max(MinWidthDip, Width + e.HorizontalChange);

    private double ExpandedMinHeightDip => _config.Kind == WidgetKind.Clock ? ClockMinHeightDip : MinHeightDip;

    private void BottomGrip_DragDelta(object sender, DragDeltaEventArgs e) => Height = Math.Max(ExpandedMinHeightDip, Height + e.VerticalChange);

    private void CornerGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidthDip, Width + e.HorizontalChange);
        Height = Math.Max(ExpandedMinHeightDip, Height + e.VerticalChange);
    }

    private void Grip_DragCompleted(object sender, DragCompletedEventArgs e) => Changed?.Invoke(this);
}
