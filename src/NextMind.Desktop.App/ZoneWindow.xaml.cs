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
using NextMind.Desktop.Core.Search;
using NextMind.Desktop.Core.Ui;
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
    private const string EmptyDropText = "Drop files, folders or shortcuts here";
    private const uint WM_WINDOWPOSCHANGING = 0x0046;
    private const int WM_MOVING = 0x0216;
    private const int WM_EXITSIZEMOVE = 0x0232;
    private const double SnapThresholdDip = 10;
    private const double SnapGapDip = 12;

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
    private IReadOnlyList<RectPx>? _snapOthers;
    private IReadOnlyList<RectPx>? _snapAreas;
    private (int CollapsedY, int ExpandedY)? _autoShift;

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

        // The title-bar menu is declared in XAML; give it the same neighbour-aware placement as the item menus.
        ContextMenuService.SetPlacement(TitleBar, PlacementMode.Custom);
        TitleBar.ContextMenu.CustomPopupPlacementCallback = (popup, _, _) => PlaceMenu(TitleBar.ContextMenu, popup);
        TitleBar.ContextMenuOpening += (_, _) => PrepareMenuForDismissal(TitleBar.ContextMenu);

        // Showing a hidden zone again must re-assert the desktop-level slot.
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && _hwnd != IntPtr.Zero)
            {
                Repin();
            }
            else if (!IsVisible)
            {
                CloseSearch();
            }
        };

        // The user went to another window while the search box had the keyboard: the zone becomes inert again at once.
        Deactivated += (_, _) =>
        {
            if (_textActivation)
            {
                _log.Info($"Search: zone deactivated; foreground moved to 0x{ForegroundWindow.Current():X} ({ForegroundWindow.Describe(ForegroundWindow.Current())}).");
            }

            EndTextActivation(restoreForeground: false);
        };
    }

    public ZoneConfig Config => _config;

    public bool IsCollapsed { get; private set; }

    /// <summary>True when the last attempt to sit directly above the desktop host succeeded.</summary>
    public bool IsPinned { get; private set; }

    /// <summary>Lists all zones (for "Move to Zone"). Set by the manager.</summary>
    public Func<IReadOnlyList<ZoneConfig>>? ZoneProvider { get; set; }

    /// <summary>Rectangles (physical pixels) of the other zones currently on screen; used for snapping, expand direction and menu placement.</summary>
    public Func<ZoneWindow, IReadOnlyList<RectPx>>? OthersProvider { get; set; }

    /// <summary>Live window rectangle in physical pixels.</summary>
    public RectPx CurrentRect => _hwnd == IntPtr.Zero ? new RectPx(_config.X, _config.Y, _config.Width, _config.Height) : Monitors.GetWindowRect(_hwnd);

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
        else if (msg == WM_MOVING)
        {
            // Magnetic edges while dragging the title bar. Alt switches it off; only the proposed position is adjusted.
            if (!Monitors.IsAltDown() && TrySnap(lParam))
            {
                handled = true;
                return (IntPtr)1; // TRUE: the rectangle was modified
            }
        }
        else if (msg == WM_EXITSIZEMOVE)
        {
            _snapOthers = null;
            _snapAreas = null;
        }

        return IntPtr.Zero;
    }

    private bool TrySnap(IntPtr rectPointer)
    {
        if (OthersProvider is null)
        {
            return false;
        }

        // Neighbours and work areas cannot change during one drag: read them once per drag, not per mouse move.
        _snapOthers ??= OthersProvider(this);
        _snapAreas ??= Monitors.GetWorkAreas();

        var dpi = CurrentDpi();
        var proposed = Monitors.ReadRect(rectPointer);
        var snapped = ZoneLayout.Snap(proposed, _snapOthers, _snapAreas, ZoneGeometry.DipToPx(SnapThresholdDip, dpi), ZoneGeometry.DipToPx(SnapGapDip, dpi));
        if (snapped == proposed)
        {
            return false;
        }

        Monitors.WriteRect(rectPointer, snapped);
        return true;
    }

    // ---------- in-zone search ----------

    private bool _searchOpen;
    private bool _textActivation;
    private long _searchPrevious;
    private bool _filterActive;
    private int _visibleMatches;

    /// <summary>True while the search box holds a query; the zone then shows only matching tiles and manual reordering is off.</summary>
    private bool IsFiltering => _filterActive;

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_searchOpen)
        {
            CloseSearch();
        }
        else
        {
            OpenSearch();
        }
    }

    /// <summary>A collapsed zone expands first (same direction logic as a normal expand), then the search bar opens and takes the keyboard.</summary>
    public void OpenSearch()
    {
        if (_searchOpen || _hwnd == IntPtr.Zero)
        {
            return;
        }

        if (IsCollapsed)
        {
            ToggleCollapsed();
        }

        _searchOpen = true;
        SearchBar.Visibility = Visibility.Visible;
        BeginTextActivation();
    }

    /// <summary>Closes the bar, clears the box and gives the keyboard back. Safe to call at any time.</summary>
    public void CloseSearch()
    {
        if (!_searchOpen)
        {
            return;
        }

        _searchOpen = false;
        SearchBox.Clear();
        SearchBar.Visibility = Visibility.Collapsed;
        Keyboard.ClearFocus();
        EndTextActivation(restoreForeground: true);
    }

    /// <summary>
    /// A zone is inert (WS_EX_NOACTIVATE) so it can never take the keyboard. For the search box only: lift that flag, ask for the foreground
    /// (allowed — the user just clicked this window) and focus the box. The z-order hook keeps the zone at desktop level even while it is active.
    /// Windows may refuse the foreground change; then nothing is forced and the zone is made inert again.
    /// </summary>
    private void BeginTextActivation()
    {
        if (_hwnd == IntPtr.Zero || _textActivation)
        {
            return;
        }

        var self = _hwnd.ToInt64();
        _searchPrevious = ForegroundWindow.Current();
        DesktopLayer.SetNoActivate(_hwnd, false);
        _textActivation = true;

        var granted = ForegroundWindow.TrySet(self);
        if (granted)
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }

        _log.Info($"Search: activation {(granted ? "granted" : "refused")}; foreground is zone={ForegroundWindow.Current() == self}, box has keyboard={SearchBox.IsKeyboardFocused}.");
        if (!granted)
        {
            EndTextActivation(restoreForeground: false);
        }
    }

    /// <summary>
    /// Makes the zone inert again. The previous window gets the foreground back only if this zone still holds it —
    /// if the user already moved on to another window, nothing is taken or given back.
    /// </summary>
    private void EndTextActivation(bool restoreForeground)
    {
        if (!_textActivation || _hwnd == IntPtr.Zero)
        {
            return;
        }

        _textActivation = false;
        DesktopLayer.SetNoActivate(_hwnd, true);

        var previous = _searchPrevious;
        _searchPrevious = 0;
        if (restoreForeground && MenuForeground.ShouldRestore(previous, _hwnd.ToInt64(), ForegroundWindow.Current()))
        {
            ForegroundWindow.TrySet(previous);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var empty = SearchBox.Text.Length == 0;
        SearchHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        SearchClear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        ApplyFilter();
    }

    /// <summary>
    /// Shows only the tiles matching the query (every word, ignoring case and Polish letters), in the zone's own order. A pure VIEW change:
    /// tiles are hidden through their container style (layout only — a collection-view filter rebuilt every container per key), the
    /// configuration, item order, icons and files are never touched. Clearing the query shows everything again.
    /// </summary>
    private void ApplyFilter()
    {
        var wasActive = _filterActive;
        var words = ZoneSearch.Words(SearchBox.Text);
        _filterActive = words.Count > 0;

        var visible = 0;
        ItemViewModel? first = null;
        foreach (var item in _items)
        {
            var hide = _filterActive && !ZoneSearch.Matches(item.SearchKey, words);
            item.IsFilteredOut = hide;
            if (!hide)
            {
                visible++;
                first ??= item;
            }
        }

        _visibleMatches = visible;

        if (ItemsList.SelectedItem is ItemViewModel { IsFilteredOut: true })
        {
            ItemsList.SelectedItem = null;
        }

        if (_filterActive && ItemsList.SelectedItem is null && first is not null)
        {
            ItemsList.SelectedItem = first; // the result Enter would open
            ItemsList.ScrollIntoView(first);
        }
        else if (!_filterActive && wasActive)
        {
            ItemsList.SelectedItem = null;
        }

        UpdateEmptyHint();
    }

    /// <summary>Up/Down walk the visible results in order; the first press selects the first (Down) or last (Up) result.</summary>
    private void MoveSelection(int delta)
    {
        var visible = _items.Where(i => !i.IsFilteredOut).ToList();
        if (visible.Count == 0)
        {
            return;
        }

        var index = ItemsList.SelectedItem is ItemViewModel current ? visible.IndexOf(current) : -1;
        var next = index < 0 ? (delta > 0 ? 0 : visible.Count - 1) : Math.Clamp(index + delta, 0, visible.Count - 1);
        ItemsList.SelectedItem = visible[next];
        ItemsList.ScrollIntoView(visible[next]);
    }

    /// <summary>Enter opens the selected result, or the first match. With no query and no selection nothing is opened.</summary>
    private void OpenSelectedResult()
    {
        var target = ItemsList.SelectedItem is ItemViewModel { IsFilteredOut: false } selected
            ? selected
            : _filterActive ? _items.FirstOrDefault(i => !i.IsFilteredOut) : null;
        if (target is not null)
        {
            OpenItem(target);
        }
    }

    /// <summary>The ✕ inside the field: clears the whole query in one click and keeps typing possible.</summary>
    private void SearchClear_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Clear();
        if (!_textActivation)
        {
            BeginTextActivation();
        }
        else
        {
            SearchBox.Focus();
        }

        e.Handled = true;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                // First Esc clears the query, the next one closes the bar.
                if (SearchBox.Text.Length > 0)
                {
                    SearchBox.Clear();
                }
                else
                {
                    CloseSearch();
                }

                e.Handled = true;
                break;

            case Key.Down:
                MoveSelection(+1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                OpenSelectedResult();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Clicking the box after the zone went inert (the user had switched windows) takes the keyboard again.</summary>
    private void SearchBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_textActivation)
        {
            BeginTextActivation();
        }
    }

    // ---------- context-menu dismissal ----------

    /// <summary>Hidden window (the message window) the menu borrows the foreground on. Set by the manager.</summary>
    public Func<IntPtr>? ForegroundAnchorProvider { get; set; }

    private ContextMenu? _preparedMenu;
    private RoutedEventHandler? _restoreForeground;

    /// <summary>
    /// A zone is never the foreground window, and WPF dismisses a menu through mouse capture and keyboard focus, which only work for the
    /// foreground window — so a menu opened here stayed up after a click elsewhere or Alt+Tab (submenus included: they share the same menu mode).
    /// While the menu is open the hidden message window holds the foreground (the tray menu uses the same technique); when it closes, the
    /// previous window gets the foreground back only if nobody has taken it since. Zones themselves are never activated or re-ordered.
    /// If Windows refuses the foreground change, nothing is forced and the menu behaves as before.
    /// </summary>
    private void PrepareMenuForDismissal(ContextMenu menu)
    {
        if (_preparedMenu is not null && _restoreForeground is not null)
        {
            _preparedMenu.Closed -= _restoreForeground; // a previous menu that never opened must not restore a stale window
            _preparedMenu = null;
            _restoreForeground = null;
        }

        var anchor = ForegroundAnchorProvider?.Invoke().ToInt64() ?? 0;
        var previous = ForegroundWindow.Current();
        if (anchor == 0 || previous == anchor || !ForegroundWindow.TrySet(anchor))
        {
            return;
        }

        _preparedMenu = menu;
        _restoreForeground = (_, _) =>
        {
            menu.Closed -= _restoreForeground;
            _preparedMenu = null;
            _restoreForeground = null;
            if (MenuForeground.ShouldRestore(previous, anchor, ForegroundWindow.Current()))
            {
                ForegroundWindow.TrySet(previous);
            }
        };
        menu.Closed += _restoreForeground;
    }

    // ---------- context-menu placement ----------

    /// <summary>Applies neighbour-aware placement to a menu that opens at the cursor (see <see cref="MenuPlacement"/>).</summary>
    private void UseSmartPlacement(ContextMenu menu)
    {
        menu.Placement = PlacementMode.Custom;
        menu.CustomPopupPlacementCallback = (popup, _, _) => PlaceMenu(menu, popup);
    }

    private CustomPopupPlacement[] PlaceMenu(ContextMenu menu, Size popupSize)
    {
        if (menu.PlacementTarget is not FrameworkElement target || PresentationSource.FromVisual(target) is not { } source)
        {
            return [new CustomPopupPlacement(new Point(0, 0), PopupPrimaryAxis.None)];
        }

        var scale = source.CompositionTarget.TransformToDevice.M11;
        var origin = target.PointToScreen(new Point(0, 0)); // physical pixels
        var size = new SizePx((int)Math.Ceiling(popupSize.Width * scale), (int)Math.Ceiling(popupSize.Height * scale));

        var candidates = MenuPlacement.Candidates(Monitors.GetCursor(), size, CurrentRect);
        var others = OthersProvider?.Invoke(this) ?? [];
        var order = MenuPlacement.Rank(candidates, Monitors.GetWorkAreas(), others);

        // WPF uses the first entry that fits on screen; the list is already ordered by "covers least".
        return order
            .Select(i => new CustomPopupPlacement(
                new Point((candidates[i].X - origin.X) / scale, (candidates[i].Y - origin.Y) / scale), PopupPrimaryAxis.None))
            .ToArray();
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

    /// <summary>
    /// Collapse keeps the zone where it is. Expand opens downward from the title bar unless that would cover other zones (or leave
    /// the screen) and opening upward is better — see <see cref="ZoneLayout.PlanExpand"/>. Other zones are never moved. If the zone
    /// opened upward, collapsing it again returns it to where its title bar was, unless the user moved it in the meantime.
    /// </summary>
    public void ToggleCollapsed()
    {
        if (_hwnd == IntPtr.Zero)
        {
            SetCollapsedCore(!IsCollapsed);
            ZoneChanged?.Invoke(this);
            return;
        }

        var current = Monitors.GetWindowRect(_hwnd);
        if (IsCollapsed)
        {
            Expand(current);
        }
        else
        {
            Collapse(current);
        }

        ZoneChanged?.Invoke(this);
    }

    private void Expand(RectPx collapsed)
    {
        var height = Math.Max(_expandedHeightPx, ZoneGeometry.DipToPx(MinHeightDip, CurrentDpi()));
        var plan = ZoneLayout.PlanExpand(collapsed, height, OthersProvider?.Invoke(this) ?? [], Monitors.GetWorkAreas());

        SetCollapsedCore(collapsed: false, applyHeight: false);
        Monitors.SetWindowBounds(_hwnd, plan);
        _autoShift = plan.Y != collapsed.Y ? (collapsed.Y, plan.Y) : null;
    }

    private void Collapse(RectPx expanded)
    {
        CloseSearch(); // a collapsed zone has no search bar; this also makes the zone inert again
        _expandedHeightPx = expanded.Height;
        var shift = _autoShift;
        _autoShift = null;

        SetCollapsedCore(collapsed: true);

        if (shift is { } s && expanded.Y == s.ExpandedY)
        {
            var collapsedHeight = ZoneGeometry.CollapsedHeightPx(CollapsedHeightDip, CurrentDpi());
            Monitors.SetWindowBounds(_hwnd, new RectPx(expanded.X, s.CollapsedY, expanded.Width, collapsedHeight));
        }
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
    {
        if (_filterActive)
        {
            EmptyHint.Text = $"No matches for “{SearchBox.Text.Trim()}”";
            EmptyHint.Visibility = _visibleMatches == 0 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        EmptyHint.Text = EmptyDropText;
        EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

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

        if (_filterActive)
        {
            ApplyFilter(); // new tiles start visible; keep the active filter consistent
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
        UseSmartPlacement(menu);
        PrepareMenuForDismissal(menu);
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
        if (IsFiltering)
        {
            _dragCandidate = null; // reordering by drag needs the full list; it is off while a filter is active
            return;
        }

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
            if (!_filterActive)
            {
                ShowDropIndicator(ComputeInsertIndex(e.GetPosition(ItemsList)));
            }

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

        if (_filterActive)
        {
            SearchBox.Clear(); // a drop shows the whole list again: the new item must be visible and indexes must match the tiles
            ItemsList.UpdateLayout();
        }

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
