using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace NextMind.Desktop.App;

public partial class ClockWidgetContent : UserControl
{
    public ClockWidgetContent()
    {
        InitializeComponent();
        ApplyLayout(0, 0);
    }

    public void UpdateClock(DateTime now)
    {
        ClockTime.Text = now.ToString("HH:mm", CultureInfo.CurrentCulture);
        ClockDate.Text = now.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture);
    }

    private void ClockWidgetContent_SizeChanged(object sender, SizeChangedEventArgs e)
        => ApplyLayout(e.NewSize.Width, e.NewSize.Height);

    private void ApplyLayout(double width, double height)
    {
        var metrics = ClockWidgetLayout.Calculate(width, height);
        ClockLabel.FontSize = metrics.LabelFontSize;
        ClockTime.FontSize = metrics.TimeFontSize;
        ClockDate.FontSize = metrics.DateFontSize;
        DateFit.Margin = new Thickness(0, metrics.DateMargin, 0, 0);
    }
}
