using System.Windows;
using System.Windows.Controls;
using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.App;

/// <summary>
/// The place to manage the program without hunting through the tray: zones (show/hide, rename), create, "Start with Windows", hide/show all, exit.
/// Opened on demand (tray double-click or "Open NextMind Desktop"); it is an ordinary window and is not kept open.
/// </summary>
public partial class ControlCenterWindow : Window
{
    private readonly ZoneManager _zones;
    private readonly Func<bool> _autostartChecked;
    private readonly Func<bool> _autostartAvailable;
    private readonly Action _toggleAutostart;
    private readonly Action _createZone;
    private readonly Action _exit;

    public ControlCenterWindow(
        ZoneManager zones,
        Func<bool> autostartChecked,
        Func<bool> autostartAvailable,
        Action toggleAutostart,
        Action createZone,
        Action exit)
    {
        _zones = zones;
        _autostartChecked = autostartChecked;
        _autostartAvailable = autostartAvailable;
        _toggleAutostart = toggleAutostart;
        _createZone = createZone;
        _exit = exit;

        InitializeComponent();
        _zones.Changed += OnZonesChanged;
        Closed += (_, _) => _zones.Changed -= OnZonesChanged;
        Refresh();
    }

    private void OnZonesChanged() => Dispatcher.BeginInvoke(Refresh);

    /// <summary>Rebuilds the rows from the current configuration.</summary>
    public void Refresh()
    {
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

    private void Create_Click(object sender, RoutedEventArgs e) => _createZone();

    private void Autostart_Click(object sender, RoutedEventArgs e)
    {
        _toggleAutostart();
        AutostartBox.IsChecked = _autostartChecked(); // show the real state, whatever happened
    }

    private void HideAll_Click(object sender, RoutedEventArgs e) => _zones.HideAll();

    private void ShowAll_Click(object sender, RoutedEventArgs e) => _zones.ShowAll();

    private void Exit_Click(object sender, RoutedEventArgs e) => _exit();
}
