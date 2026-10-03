using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Core.Config;

public static class ZoneFactory
{
    public const string FirstZoneTitle = "🦈 NextMind";

    /// <summary>
    /// A new zone placed near the top-right of <paramref name="workArea"/>, sized in device-independent
    /// units (320 x 220) scaled to <paramref name="dpi"/>. <paramref name="index"/> cascades further zones.
    /// </summary>
    public static ZoneConfig Create(string title, RectPx workArea, int dpi, int index = 0)
    {
        var width = ZoneGeometry.DipToPx(320, dpi);
        var height = ZoneGeometry.DipToPx(220, dpi);
        var margin = ZoneGeometry.DipToPx(48, dpi);
        var step = ZoneGeometry.DipToPx(36, dpi) * index;

        var rect = ZoneGeometry.ClampToWorkAreas(
            new RectPx(workArea.Right - width - margin - step, workArea.Y + margin + step, width, height),
            [workArea]);

        return new ZoneConfig
        {
            Title = title,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            Collapsed = false,
            Dpi = dpi,
        };
    }
}
