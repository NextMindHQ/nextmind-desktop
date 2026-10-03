using System.Collections.ObjectModel;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Shell;

namespace NextMind.Desktop.App;

/// <summary>
/// One desktop zone. A small borderless, non-activating tool window pinned at desktop level by <see cref="DesktopLayer"/>:
/// no Alt+Tab entry, no taskbar button, never takes focus, never above ordinary windows.
/// This class never touches the disk beyond read-only probes: dropped files are handed to the manager (which decides between a
/// reference and a managed move), reordering and "Move to Zone" only edit configuration. Drops of real files are answered with the
/// Link effect — a Move effect would make Explorer delete the source itself.
/// </summary>
public partial class ZoneWindow : Window
{
    /// <summary>Title row (30) + the 1 px border on each side.</summary>
    public const double CollapsedHeightDip = 32;

    private const double MinWidthDip = 160;
    private const double MinHeightDip = 80;
    private const double IconDip = 32;
    private const uint WM_WINDOWPOSCHANGING = 0x0046;

    private readonly ZoneConfig _config;
    private readonly DesktopLayer _layer;
    private readonly ILog _log;
    private readonly IconService _icons;
    private readonly ObservableCollection<ItemViewModel> _items = [];
    private HwndSource? _source;
    private IntPtr _hwnd;
    private int _expandedHeightPx;
    private bool _initialised;
    private Point? _dragStart;
    private ItemViewModel? _dragCandidate;
    private ListBoxItem? _indicatorTarget;

    public ZoneWindow(ZoneConfig config, DesktopLayer layer, ILog log, IconService icons)
    {
        _config = config;
        _layer = layer;
        _log = log;
        _icons = icons;

        InitializeComponent();
        TitleText.Text = config.Title;
        _expandedHeightPx = config.Height;

        foreach (var item in ZoneSorter.Order(config))
        {
            _items.Add(NewViewModel(item));
        }

        ItemsList.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => UpdateEmptyHint();
        UpdateEmptyHint();

        // Showing a hidden zone again must re-assert the desktop-level slot.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && _hwnd != IntPtr.Zero)
            {
                Repin();
            }
        };
    }

    public ZoneConfig Config => _config;

    public bool IsCollapsed { get; private set; }

    /// <summary>True when the last attempt to sit directly above the desktop host succeeded.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Lists all zones (for "Move to Zone"). Set by the manager.</summary>
    public Func<IReadOnlyList<ZoneConfig>>? ZoneProvider { get; set; }

    /// <summary>Raised after any user-visible geometry/collapse/item change; the manager debounces persistence.</summary>
    public event Action<ZoneWindow>? ZoneChanged;

    public event Action<ZoneWindow>? RenameRequested;

    public event Action<ZoneWindow>? HideRequested;

    public event Action<ZoneWindow>? DeleteRequested;

    /// <summary>Real files were dropped. The manager classifies them (Desktop item vs external) and decides reference vs managed move.</summary>
    public event Action<ZoneWindow, IReadOnlyList<string>>? PathsDropped;

    public event Action<ZoneWindow, ItemConfig>? ReturnToDesktopRequested;

    public event Action<ZoneWindow, ItemConfig, string>? MoveToZoneRequested;

    /// <summary>An item dragged from another zone was dropped here at the given display index.</summary>
    public event Action<ZoneWindow, ZoneItemDragPayload, int>? ItemDroppedFromOtherZone;

    public void SetTitle(string title) => TitleText.Text = title;

    /// <summary>Captures the live geometry into this zone's config (used right before hiding).</summary>
    public void Snapshot() => SnapshotInto(_config);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        // Before the window is first shown: no taskbar button / Alt+Tab entry, non-activating.
        DesktopLayer.ApplyDesktopWindowStyles(_hwnd);

        ApplyConfigGeometry();
        Repin();
        if (!IsPinned)
        {
            _log.Warn($"Zone '{_config.Title}': desktop host not found yet, visible fallback z-order for now. {_layer.Describe()}");
        }

        LoadIcons();
        _initialised = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _source?.RemoveHook(WndProc);
        _source = null;
        base.OnClosed(e);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_initialised)
        {
            ZoneChanged?.Invoke(this);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // Keep the title from pushing the buttons out of a narrow zone.
        TitleText.MaxWidth = Math.Max(40, sizeInfo.NewSize.Width - 130);

        if (!_initialised)
        {
            return;
        }

        if (!IsCollapsed && _hwnd != IntPtr.Zero)
        {
            _expandedHeightPx = Monitors.GetWindowRect(_hwnd).Height;
        }

        ZoneChanged?.Invoke(this);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        LoadIcons();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            // Let the message continue with a corrected z-order so WPF still sees the move/resize.
            _layer.EnforceOnWindowPosChanging(hwnd, lParam);
        }

        return IntPtr.Zero;
    }

    /// <summary>Re-asserts the desktop-level z-order slot. Returns false while the desktop host is not available.</summary>
    public bool Repin()
    {
        IsPinned = _hwnd != IntPtr.Zero && _layer.Pin(_hwnd);
        return IsPinned;
    }

    /// <summary>Pulls the zone back inside the visible work areas (e.g. after a monitor was unplugged).</summary>
    public void ClampToWorkAreas(IReadOnlyList<RectPx> workAreas)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var current = Monitors.GetWindowRect(_hwnd);
        var clamped = ZoneGeometry.ClampToWorkAreas(current, workAreas);
        if (clamped != current)
        {
            Monitors.SetWindowBounds(_hwnd, clamped);
        }
    }

    /// <summary>Writes the live geometry and collapsed state into the config object (physical pixels, expanded height).</summary>
    public void SnapshotInto(ZoneConfig target)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var r = Monitors.GetWindowRect(_hwnd);
        target.X = r.X;
        target.Y = r.Y;
        target.Width = r.Width;
        target.Height = IsCollapsed ? _expandedHeightPx : r.Height;
        target.Collapsed = IsCollapsed;
        target.Dpi = Monitors.GetDpi(_hwnd);
    }

    private void ApplyConfigGeometry()
    {
        var wanted = new RectPx(_config.X, _config.Y, _config.Width, _config.Height);
        var rect = ZoneGeometry.ClampToWorkAreas(wanted, Monitors.GetWorkAreas());

        _expandedHeightPx = rect.Height;
        Monitors.SetWindowBounds(_hwnd, rect);

        if (_config.Collapsed)
        {
            SetCollapsedCore(collapsed: true);
        }
        else
        {
            SetCollapsedCore(collapsed: false, applyHeight: false);
        }
    }

    public void ToggleCollapsed()
    {
        if (!IsCollapsed && _hwnd != IntPtr.Zero)
        {
            _expandedHeightPx = Monitors.GetWindowRect(_hwnd).Height;
        }

        SetCollapsedCore(!IsCollapsed);
        ZoneChanged?.Invoke(this);
    }

    private void SetCollapsedCore(bool collapsed, bool applyHeight = true)
    {
        IsCollapsed = collapsed;

        ContentArea.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        RightGrip.Visibility = BottomGrip.Visibility = CornerGrip.Visibility = CornerMark.Visibility =
            collapsed ? Visibility.Collapsed : Visibility.Visible;
        MinButton.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        ArrowRotation.Angle = collapsed ? 0 : 90; // ▶ when collapsed, ▼ when expanded; position never changes
        ToggleArrow.ToolTip = collapsed ? "Expand" : "Collapse";
        TitleBar.CornerRadius = collapsed ? new CornerRadius(9) : new CornerRadius(9, 9, 0, 0);

        if (!applyHeight)
        {
            return;
        }

        if (collapsed)
        {
            Height = CollapsedHeightDip;
        }
        else
        {
            var dpi = CurrentDpi();
            Height = Math.Max(MinHeightDip, ZoneGeometry.PxToDip(_expandedHeightPx, dpi));
        }
    }

    // ---------- items ----------

    private int CurrentDpi() => _hwnd == IntPtr.Zero ? Monitors.GetSystemDpi() : Monitors.GetDpi(_hwnd);

    private int IconSizePx() => ZoneGeometry.DipToPx(IconDip, CurrentDpi());

    private ItemViewModel NewViewModel(ItemConfig item)
    {
        var vm = new ItemViewModel(item);
        vm.FolderProbed += OnFolderProbed;
        return vm;
    }

    /// <summary>The background probe learned whether an old (pre-M3) item is a folder: remember it, and re-sort if the order depends on it.</summary>
    private void OnFolderProbed(ItemViewModel vm, bool isDirectory)
    {
        if (vm.Config.IsFolder is null)
        {
            vm.Config.IsFolder = isDirectory;
            if (_config.SortMode == SortMode.Type)
            {
                SyncItems();
            }

            ZoneChanged?.Invoke(this);
        }
    }

    private void LoadIcons()
    {
        var size = IconSizePx();
        foreach (var item in _items)
        {
            _icons.Request(item, size);
        }
    }

    private void UpdateEmptyHint()
        => EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Makes the displayed grid match the configuration (membership, custom order and sort mode). Existing tiles are reused, so icons do not reload.
    /// </summary>
    public void SyncItems()
    {
        var desired = ZoneSorter.Order(_config);

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (!desired.Any(d => string.Equals(d.Id, _items[i].Id, StringComparison.OrdinalIgnoreCase)))
            {
                _items.RemoveAt(i);
            }
        }

        var size = IconSizePx();
        for (var i = 0; i < desired.Count; i++)
        {
            var existing = IndexOfItem(desired[i].Id);
            if (existing < 0)
            {
                var vm = NewViewModel(desired[i]);
                _items.Insert(i, vm);
                _icons.Request(vm, size);
            }
            else if (existing != i)
            {
                _items.Move(existing, i);
            }
        }
    }

    private int IndexOfItem(string id)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Adds a REFERENCE to <paramref name="path"/>; the target is not touched. A duplicate just selects the existing tile.</summary>
    public AddItemResult AddReference(string path)
    {
        string? name = null;
        try
        {
            name = ShellIcons.TryGetDisplayName(path);
        }
        catch (Exception ex)
        {
            _log.Warn($"Display name lookup failed: {ex.Message}");
        }

        var result = ZoneItems.TryAddReference(_config, path, name, ShellActions.IsDirectory(path), DateTime.UtcNow, out var item);
        if (result == AddItemResult.Invalid || item is null)
        {
            _log.Warn($"Ignored an item that is not a usable absolute path: '{path}'.");
            return result;
        }

        if (result == AddItemResult.Added)
        {
            SyncItems();
            _log.Info($"Zone '{_config.Title}': added a reference; {_config.Items.Count} item(s).");
            ZoneChanged?.Invoke(this);
        }

        SelectItem(item.Id);
        return result;
    }

    /// <summary>Call after the manager added/removed items in this zone's config (managed move, return to Desktop, move between zones).</summary>
    public void ItemsChangedExternally()
    {
        SyncItems();
        ZoneChanged?.Invoke(this);
    }

    private void SelectItem(string id)
    {
        var index = IndexOfItem(id);
        if (index >= 0)
        {
            ItemsList.SelectedItem = _items[index];
            ItemsList.ScrollIntoView(_items[index]);
        }
    }

    private static ItemViewModel? ItemFrom(object sender)
        => (sender as FrameworkElement)?.DataContext as ItemViewModel;

    private void OpenItem(ItemViewModel item)
    {
        if (item.IsMissing)
        {
            _icons.Request(item, IconSizePx()); // maybe it is back: re-probe
            SystemSounds.Beep.Play();
            return;
        }

        if (!ShellActions.TryOpen(item.Path, out var error))
        {
            _log.Warn($"Could not open '{item.Name}': {error}");
            SystemSounds.Beep.Play();
        }
    }

    private void Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemFrom(sender) is { } item)
        {
            OpenItem(item);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The item menu is built here, not in XAML: WPF cannot wire Click handlers of a ContextMenu that lives inside a Style.
    /// A reference offers "Remove from Zone" (config only). A managed item NEVER offers a plain remove (that would orphan its data):
    /// it offers "Return to Desktop" and "Show Managed Location" instead. There is deliberately no delete command at all.
    /// </summary>
    private void Item_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: ItemViewModel item } container)
        {
            return;
        }

        ItemsList.SelectedItem = item;

        var menu = new ContextMenu();
        menu.Items.Add(NewMenuItem("Open", () => OpenItem(item)));

        if (item.IsManaged)
        {
            menu.Items.Add(NewMenuItem("Return to Desktop", () => ReturnToDesktopRequested?.Invoke(this, item.Config)));
            menu.Items.Add(BuildMoveToZoneMenu(item));
            menu.Items.Add(NewMenuItem("Show Managed Location", () => ShowInExplorer(item)));
        }
        else
        {
            menu.Items.Add(NewMenuItem("Show in Explorer", () => ShowInExplorer(item)));
            menu.Items.Add(BuildMoveToZoneMenu(item));
            menu.Items.Add(new Separator());
            menu.Items.Add(NewMenuItem("Remove from Zone", () => RemoveReference(item)));
        }

        // Setting ContextMenu from inside ContextMenuOpening is too late for WPF to show it: open it explicitly.
        menu.PlacementTarget = container;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private MenuItem BuildMoveToZoneMenu(ItemViewModel item)
    {
        var parent = new MenuItem { Header = "Move to Zone" };
        var others = (ZoneProvider?.Invoke() ?? []).Where(z => !string.Equals(z.Id, _config.Id, StringComparison.OrdinalIgnoreCase)).ToList();

        if (others.Count == 0)
        {
            parent.Items.Add(new MenuItem { Header = "(no other zones)", IsEnabled = false });
            return parent;
        }

        foreach (var zone in others)
        {
            var target = zone.Id;
            var entry = new MenuItem { Header = new TextBlock { Text = zone.Title } }; // a TextBlock: a plain string would treat '_' as an access key
            entry.Click += (_, _) => MoveToZoneRequested?.Invoke(this, item.Config, target);
            parent.Items.Add(entry);
        }

        return parent;
    }

    private static MenuItem NewMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    private void ShowInExplorer(ItemViewModel item)
    {
        if (!ShellActions.TryShowInExplorer(item.Path, out var error))
        {
            _log.Warn($"Show in Explorer failed for '{item.Name}': {error}");
            SystemSounds.Beep.Play();
        }
    }

    /// <summary>Removes a REFERENCE only: no file is touched, moved or sent to the Recycle Bin.</summary>
    private void RemoveReference(ItemViewModel item)
    {
        if (item.IsManaged)
        {
            return; // defence in depth: the menu never offers this for a managed item
        }

        if (ZoneItems.Remove(_config, item.Id))
        {
            SyncItems();
            _log.Info($"Zone '{_config.Title}': removed reference '{item.Name}'; {_config.Items.Count} left.");
            ZoneChanged?.Invoke(this);
        }
    }

    // ---------- sort mode ----------

    private void ZoneMenu_Opened(object sender, RoutedEventArgs e)
    {
        foreach (var entry in SortMenu.Items.OfType<MenuItem>())
        {
            entry.IsChecked = entry.Tag is string tag && tag == _config.SortMode.ToString();
        }
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && Enum.TryParse<SortMode>(tag, out var mode))
        {
            ZoneOrdering.SetSortMode(_config, mode);
            SyncItems();
            ZoneChanged?.Invoke(this);
        }
    }

    // ---------- manual ordering (drag inside the zone) ----------

    private void Items_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ItemsList);
        _dragCandidate = e.OriginalSource is DependencyObject source
            ? ItemsControl.ContainerFromElement(ItemsList, source) is ListBoxItem { DataContext: ItemViewModel vm } ? vm : null
            : null;
    }

    private void Items_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragCandidate is null || _dragStart is null)
        {
            _dragCandidate = null;
            return;
        }

        var delta = e.GetPosition(ItemsList) - _dragStart.Value;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _dragCandidate;
        _dragCandidate = null;

        // A reordering drag: private data only (see ZoneItemDrag). It can only change configuration.
        ZoneItemDrag.Begin(ItemsList, new ZoneItemDragPayload(_config.Id, item.Id));
        ClearDropIndicator();
    }

    /// <summary>Display index where a drop at <paramref name="p"/> (relative to the item list) would insert (0..Count).</summary>
    private int ComputeInsertIndex(Point p)
    {
        var rects = new List<Rect?>();
        for (var i = 0; i < _items.Count; i++)
        {
            if (ItemsList.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container)
            {
                rects.Add(new Rect(container.TranslatePoint(new Point(0, 0), ItemsList), container.RenderSize));
            }
            else
            {
                rects.Add(null);
            }
        }

        for (var i = 0; i < rects.Count; i++)
        {
            if (rects[i] is { } r && r.Contains(p))
            {
                return p.X < r.X + r.Width / 2 ? i : i + 1;
            }
        }

        for (var i = 0; i < rects.Count; i++)
        {
            if (rects[i] is { } r && (p.Y < r.Top || (p.Y < r.Bottom && p.X < r.Left)))
            {
                return i;
            }
        }

        return _items.Count;
    }

    private void ShowDropIndicator(int index)
    {
        ClearDropIndicator();
        if (index >= 0 && index < _items.Count && ItemsList.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem target)
        {
            target.BorderBrush = (Brush)FindResource("DropBorderBrush");
            _indicatorTarget = target;
        }
    }

    private void ClearDropIndicator()
    {
        if (_indicatorTarget is not null)
        {
            _indicatorTarget.BorderBrush = Brushes.Transparent;
            _indicatorTarget = null;
        }
    }

    private void ReorderTo(string itemId, int insertIndex)
    {
        var from = IndexOfItem(itemId);
        if (from < 0)
        {
            return;
        }

        var to = insertIndex > from ? insertIndex - 1 : insertIndex;
        if (to == from && _config.SortMode == SortMode.Custom)
        {
            return;
        }

        // Dragging in a sorted zone switches it to Custom and keeps the arrangement the user just made.
        ZoneOrdering.MoveItem(_config, itemId, to);
        SyncItems();
        ZoneChanged?.Invoke(this);
    }

    // ---------- drag & drop ----------

    private static bool TryGetDroppedPaths(IDataObject data, out string[] paths)
    {
        paths = [];
        if (data.GetDataPresent(DataFormats.FileDrop) && data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            paths = files;
            return true;
        }

        return false;
    }

    private void Zone_DragEnter(object sender, DragEventArgs e) => HandleDrag(e);

    private void Zone_DragOver(object sender, DragEventArgs e) => HandleDrag(e);

    private void Zone_DragLeave(object sender, DragEventArgs e)
    {
        SetDropHighlight(false);
        ClearDropIndicator();
    }

    private void HandleDrag(DragEventArgs e)
    {
        e.Handled = true;

        if (ZoneItemDrag.TryGet(e.Data, out _))
        {
            e.Effects = ZoneItemDrag.InternalEffect; // configuration-only drag between/inside zones
            SetDropHighlight(false);
            ShowDropIndicator(ComputeInsertIndex(e.GetPosition(ItemsList)));
            return;
        }

        // Real files: Link, never Move — the source (Explorer/Desktop) must not delete or relocate what is dropped.
        var ok = TryGetDroppedPaths(e.Data, out _);
        e.Effects = ok ? DragDropEffects.Link : DragDropEffects.None;
        ClearDropIndicator();
        SetDropHighlight(ok);
    }

    private void Zone_Drop(object sender, DragEventArgs e)
    {
        SetDropHighlight(false);
        e.Handled = true;

        if (ZoneItemDrag.TryGet(e.Data, out var payload))
        {
            var index = ComputeInsertIndex(e.GetPosition(ItemsList));
            ClearDropIndicator();
            e.Effects = ZoneItemDrag.InternalEffect;

            if (string.Equals(payload.ZoneId, _config.Id, StringComparison.OrdinalIgnoreCase))
            {
                ReorderTo(payload.ItemId, index);
            }
            else
            {
                ItemDroppedFromOtherZone?.Invoke(this, payload, index);
            }

            return;
        }

        ClearDropIndicator();
        if (TryGetDroppedPaths(e.Data, out var paths))
        {
            e.Effects = DragDropEffects.Link;
            PathsDropped?.Invoke(this, paths); // the manager decides: reference or (when enabled) managed move
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void SetDropHighlight(bool on)
        => Root.BorderBrush = (Brush)FindResource(on ? "DropBorderBrush" : "PanelBorderBrush");

    // ---------- title bar ----------

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleCollapsed();
            e.Handled = true;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove(); // system move loop; returns when the button is released
            }
            catch (InvalidOperationException)
            {
                // Button was released before the loop started; nothing to do.
            }
        }
    }

    private void ToggleArrow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ToggleCollapsed();
        e.Handled = true;
    }

    private void MinButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsCollapsed)
        {
            ToggleCollapsed();
        }

        e.Handled = true;
    }

    private void CloseButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HideRequested?.Invoke(this);
        e.Handled = true;
    }

    private void Rename_Click(object sender, RoutedEventArgs e) => RenameRequested?.Invoke(this);

    private void Hide_Click(object sender, RoutedEventArgs e) => HideRequested?.Invoke(this);

    private void Delete_Click(object sender, RoutedEventArgs e) => DeleteRequested?.Invoke(this);

    private void RightGrip_DragDelta(object sender, DragDeltaEventArgs e)
        => Width = Math.Max(MinWidthDip, Width + e.HorizontalChange);

    private void BottomGrip_DragDelta(object sender, DragDeltaEventArgs e)
        => Height = Math.Max(MinHeightDip, Height + e.VerticalChange);

    private void CornerGrip_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width = Math.Max(MinWidthDip, Width + e.HorizontalChange);
        Height = Math.Max(MinHeightDip, Height + e.VerticalChange);
    }

    private void Grip_DragCompleted(object sender, DragCompletedEventArgs e) => ZoneChanged?.Invoke(this);
}
