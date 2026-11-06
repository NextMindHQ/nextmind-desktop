using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Widgets;

namespace NextMind.Desktop.App;

/// <summary>
/// The place to manage the program without hunting through the tray: zones (show/hide, rename), create, "Start with Windows", hide/show all, exit.
/// Opened on demand (tray double-click or "Open NextMind Desktop"); it is an ordinary window and is not kept open.
/// </summary>
public partial class ControlCenterWindow : Window
{
    private readonly ZoneManager _zones;
    private readonly WidgetManager _widgets;
    private readonly Func<bool> _autostartChecked;
    private readonly Func<bool> _autostartAvailable;
    private readonly Action _toggleAutostart;
    private readonly Action _createZone;
    private readonly Action _openSearch;
    private readonly Action _exit;

    public ControlCenterWindow(
        ZoneManager zones,
        WidgetManager widgets,
        Func<bool> autostartChecked,
        Func<bool> autostartAvailable,
        Action toggleAutostart,
        Action createZone,
        Action openSearch,
        Action exit)
    {
        _zones = zones;
        _widgets = widgets;
        _autostartChecked = autostartChecked;
        _autostartAvailable = autostartAvailable;
        _toggleAutostart = toggleAutostart;
        _createZone = createZone;
        _openSearch = openSearch;
        _exit = exit;

        InitializeComponent();
        _zones.Changed += OnZonesChanged;
        _widgets.Changed += OnZonesChanged;
        Closed += (_, _) =>
        {
            _zones.Changed -= OnZonesChanged;
            _widgets.Changed -= OnZonesChanged;
        };
        Refresh();
    }

    private void OnZonesChanged() => Dispatcher.BeginInvoke(Refresh);

    /// <summary>Rebuilds the rows from the current configuration.</summary>
    public void Refresh()
    {
        ProfileRows.Children.Clear();
        foreach (var profile in _zones.Profiles.ToList())
        {
            ProfileRows.Children.Add(BuildProfileRow(profile));
        }

        WidgetCatalogButtons.Children.Clear();
        foreach (var definition in WidgetCatalog.Definitions)
        {
            var button = new Button { Content = "+  " + definition.Title, Margin = new Thickness(0, 0, 8, 6), ToolTip = definition.Description };
            button.Click += (_, _) => _widgets.Add(definition.Kind);
            WidgetCatalogButtons.Children.Add(button);
        }

        WidgetRows.Children.Clear();
        foreach (var widget in _widgets.Widgets)
        {
            WidgetRows.Children.Add(BuildWidgetRow(widget));
        }

        ZoneRows.Children.Clear();

        foreach (var zone in _zones.Zones.ToList())
        {
            ZoneRows.Children.Add(BuildRow(zone));
        }

        if (_zones.Zones.Count == 0)
        {
            ZoneRows.Children.Add(new TextBlock { Text = "No zones yet.", Foreground = System.Windows.Media.Brushes.Gray, Margin = new Thickness(0, 4, 0, 0) });
        }

        AutostartBox.IsEnabled = _autostartAvailable();
        AutostartBox.IsChecked = _autostartChecked();
        SnapElementsBox.IsChecked = _zones.Layout.SnapToElements;
        SnapGridBox.IsChecked = _zones.Layout.SnapToGrid;
        GridSizeBox.Text = _zones.Layout.GridSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
        GapBox.Text = _zones.Layout.Gap.ToString(System.Globalization.CultureInfo.InvariantCulture);
        UndoMoveButton.IsEnabled = _zones.CanUndoMove;
        GateText.Text = _zones.ManagedMovesEnabled
            ? "Managed Desktop Moves: ENABLED. Items you drop from your Desktop are moved into the zone."
            : "Managed Desktop Moves: DISABLED (safety gate). Items dropped from your Desktop are added as references and stay on the Desktop.";
    }

    private UIElement BuildRow(ZoneConfig zone)
    {
        var shown = _zones.IsShown(zone);
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = zone.Title,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = shown ? 1 : 0.55,
            ToolTip = $"{zone.Items.Count} item(s)",
        };
        Grid.SetColumn(title, 0);

        var toggle = new Button { Content = shown ? "Hide" : "Show", MinWidth = 64 };
        var id = zone.Id;
        toggle.Click += (_, _) => _zones.ToggleZoneShown(id);
        Grid.SetColumn(toggle, 1);

        var rename = new Button { Content = "Rename", MinWidth = 70 };
        rename.Click += (_, _) => _zones.RenameZoneById(id);
        Grid.SetColumn(rename, 2);

        grid.Children.Add(title);
        grid.Children.Add(toggle);
        grid.Children.Add(rename);
        return grid;
    }

    private UIElement BuildProfileRow(WorkspaceProfileConfig profile)
    {
        var active = string.Equals(profile.Id, _zones.ActiveProfileId, StringComparison.OrdinalIgnoreCase);
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var i = 0; i < 4; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        var title = new TextBlock
        {
            Text = active ? $"{profile.Name}  • Active" : profile.Name,
            FontSize = 14,
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(title, 0);
        grid.Children.Add(title);

        var switchButton = new Button { Content = "Open", IsEnabled = !active };
        switchButton.Click += (_, _) => _zones.SwitchProfile(profile.Id);
        Grid.SetColumn(switchButton, 1);
        grid.Children.Add(switchButton);

        var rename = new Button { Content = "Rename" };
        rename.Click += (_, _) => RenameProfile(profile);
        Grid.SetColumn(rename, 2);
        grid.Children.Add(rename);

        var duplicate = new Button { Content = "Duplicate" };
        duplicate.Click += (_, _) => DuplicateProfile(profile);
        Grid.SetColumn(duplicate, 3);
        grid.Children.Add(duplicate);

        var delete = new Button { Content = "Delete", IsEnabled = _zones.Profiles.Count > 1 };
        delete.Click += (_, _) => DeleteProfile(profile);
        Grid.SetColumn(delete, 4);
        grid.Children.Add(delete);
        return grid;
    }

    private UIElement BuildWidgetRow(WidgetConfig widget)
    {
        var title = WidgetCatalog.Definitions.Single(definition => definition.Kind == widget.Kind).Title;
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            Text = $"• {title}{(widget.Hidden ? " (hidden)" : widget.Collapsed ? " (collapsed)" : string.Empty)}",
            Foreground = System.Windows.Media.Brushes.LightGray,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(label);
        if (widget.Hidden)
        {
            var show = new Button { Content = "Show" };
            show.Click += (_, _) => _widgets.Show(widget.Id);
            Grid.SetColumn(show, 1);
            grid.Children.Add(show);
        }

        return grid;
    }

    private void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptWindow.AskText("New Workspace Profile", "Profile name:", "Work", "Create");
        if (name is not null)
        {
            _zones.CreateProfile(name);
        }
    }

    private void RenameProfile(WorkspaceProfileConfig profile)
    {
        var name = PromptWindow.AskText("Rename Workspace Profile", "Profile name:", profile.Name, "Rename");
        if (name is not null)
        {
            _zones.RenameProfile(profile.Id, name);
        }
    }

    private void DuplicateProfile(WorkspaceProfileConfig profile)
    {
        var name = PromptWindow.AskText("Duplicate Workspace Profile", "Copy name:", profile.Name + " Copy", "Duplicate");
        if (name is not null)
        {
            _zones.DuplicateProfile(profile.Id, name);
        }
    }

    private void DeleteProfile(WorkspaceProfileConfig profile)
    {
        if (!PromptWindow.Confirm("Delete Workspace Profile", $"Delete profile \"{profile.Name}\"?\nManaged items are never deleted.", "Delete Profile"))
        {
            return;
        }

        if (!_zones.DeleteProfile(profile.Id))
        {
            ChoiceWindow.Inform("Profile not deleted", "Keep at least one profile. Profiles containing managed Desktop items cannot be deleted; return those items first.");
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e) => _createZone();

    private void Search_Click(object sender, RoutedEventArgs e) => _openSearch();

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _openSearch();
            e.Handled = true;
        }
    }

    private void Autostart_Click(object sender, RoutedEventArgs e)
    {
        _toggleAutostart();
        AutostartBox.IsChecked = _autostartChecked(); // show the real state, whatever happened
    }

    private void HideAll_Click(object sender, RoutedEventArgs e) => _zones.HideAll();

    private void ShowAll_Click(object sender, RoutedEventArgs e) => _zones.ShowAll();

    private void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        var grid = int.TryParse(GridSizeBox.Text, out var parsedGrid) ? parsedGrid : _zones.Layout.GridSize;
        var gap = int.TryParse(GapBox.Text, out var parsedGap) ? parsedGap : _zones.Layout.Gap;
        _zones.UpdateLayoutPreferences(SnapElementsBox.IsChecked == true, SnapGridBox.IsChecked == true, grid, gap);
        Refresh();
    }

    private void AutoArrange_Click(object sender, RoutedEventArgs e) => _zones.AutoArrange();

    private void UndoMove_Click(object sender, RoutedEventArgs e) => _zones.UndoLastMove();

    private void Exit_Click(object sender, RoutedEventArgs e) => _exit();
}
