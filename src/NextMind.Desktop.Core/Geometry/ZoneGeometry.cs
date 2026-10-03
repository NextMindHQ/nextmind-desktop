using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Core.Geometry;

/// <summary>Integer rectangle in physical pixels of the virtual screen (may have negative X/Y on multi-monitor setups).</summary>
public readonly record struct RectPx(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public long IntersectionArea(RectPx other)
    {
        var w = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        var h = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        return w <= 0 || h <= 0 ? 0 : (long)w * h;
    }
}

public static class ZoneGeometry
{
    /// <summary>
    /// Moves/shrinks <paramref name="rect"/> so it lies fully inside the work area it overlaps most
    /// (or the nearest one when it is entirely off-screen, e.g. after a monitor was unplugged).
    /// With no known work areas the rectangle is returned unchanged.
    /// </summary>
    public static RectPx ClampToWorkAreas(RectPx rect, IReadOnlyList<RectPx> workAreas)
    {
        if (workAreas.Count == 0)
        {
            return rect;
        }

        var best = workAreas[0];
        var bestOverlap = -1L;
        var bestDistance = long.MaxValue;

        foreach (var area in workAreas)
        {
            var overlap = rect.IntersectionArea(area);
            var dx = (long)(rect.X + rect.Width / 2) - (area.X + area.Width / 2);
            var dy = (long)(rect.Y + rect.Height / 2) - (area.Y + area.Height / 2);
            var distance = dx * dx + dy * dy;

            if (overlap > bestOverlap || (overlap == bestOverlap && distance < bestDistance))
            {
                best = area;
                bestOverlap = overlap;
                bestDistance = distance;
            }
        }

        var width = Math.Min(rect.Width, best.Width);
        var height = Math.Min(rect.Height, best.Height);
        var x = Math.Clamp(rect.X, best.X, best.Right - width);
        var y = Math.Clamp(rect.Y, best.Y, best.Bottom - height);
        return new RectPx(x, y, width, height);
    }

    /// <summary>Pixel height of a collapsed (title-bar-only) zone at the given DPI.</summary>
    public static int CollapsedHeightPx(double collapsedHeightDip, int dpi)
        => (int)Math.Round(collapsedHeightDip * dpi / ZoneLimits.DefaultDpi, MidpointRounding.AwayFromZero);

    public static int DipToPx(double dip, int dpi)
        => (int)Math.Round(dip * dpi / ZoneLimits.DefaultDpi, MidpointRounding.AwayFromZero);

    public static double PxToDip(int px, int dpi)
        => px * (double)ZoneLimits.DefaultDpi / dpi;
}
