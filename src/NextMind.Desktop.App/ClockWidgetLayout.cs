namespace NextMind.Desktop.App;

public readonly record struct ClockWidgetMetrics(
    double LabelFontSize,
    double TimeFontSize,
    double DateFontSize,
    double DateMargin,
    double EstimatedTextHeight);

public static class ClockWidgetLayout
{
    private const double CompactWidthDip = 136;
    private const double CompactHeightDip = 60;
    private const double LineHeightEstimate = 1.2;

    public static ClockWidgetMetrics Calculate(double widthDip, double heightDip)
    {
        var width = Normalize(widthDip, CompactWidthDip);
        var height = Normalize(heightDip, CompactHeightDip);
        var time = Math.Clamp(Math.Min(width / 4.2, height * 0.45), 26, 240);
        var label = Math.Clamp(time * 0.24, 9, 48);
        var date = Math.Clamp(time * 0.34, 11, 68);
        var dateMargin = Math.Clamp(time * 0.10, 3, 20);
        var estimatedHeight = (label + time + date) * LineHeightEstimate + dateMargin;

        return new ClockWidgetMetrics(label, time, date, dateMargin, estimatedHeight);
    }

    private static double Normalize(double value, double minimum)
        => double.IsFinite(value) ? Math.Max(minimum, value) : minimum;
}
