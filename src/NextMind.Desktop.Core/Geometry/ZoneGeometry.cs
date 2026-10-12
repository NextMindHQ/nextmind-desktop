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

        var best = BestArea(rect, workAreas);
        var width = Math.Min(rect.Width, best.Width);
        var height = Math.Min(rect.Height, best.Height);
        var x = Math.Clamp(rect.X, best.X, best.Right - width);
        var y = Math.Clamp(rect.Y, best.Y, best.Bottom - height);
        return new RectPx(x, y, width, height);
    }

    /// <summary>The work area <paramref name="rect"/> overlaps most (the nearest one when it is entirely off-screen). Needs at least one area.</summary>
    private static RectPx BestArea(RectPx rect, IReadOnlyList<RectPx> workAreas)
    {
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

        return best;
    }

    /// <summary>Where a zone appears at startup, and the expanded height it remembers.</summary>
    public readonly record struct RestoredBounds(RectPx Visible, int ExpandedHeight);

    /// <summary>
    /// Restores a saved zone (<paramref name="saved"/>.Height is always the EXPANDED height). An expanded zone is clamped to the work area
    /// as a whole, exactly as before. A collapsed zone is only its title bar, so only the bar (<paramref name="collapsedHeightPx"/> tall)
    /// has to fit: clamping the saved expanded height instead pushed a collapsed zone near the bottom edge upward on every restart.
    /// The remembered expanded height is kept (capped to what the monitor can show) for the next expand, which picks its own direction.
    /// </summary>
    public static RestoredBounds RestoreBounds(RectPx saved, bool collapsed, int collapsedHeightPx, IReadOnlyList<RectPx> workAreas)
    {
        if (!collapsed)
        {
            var expanded = ClampToWorkAreas(saved, workAreas);
            return new RestoredBounds(expanded, expanded.Height);
        }

        var bar = ClampToWorkAreas(new RectPx(saved.X, saved.Y, saved.Width, collapsedHeightPx), workAreas);
        var expandedHeight = workAreas.Count == 0 ? saved.Height : Math.Min(saved.Height, BestArea(bar, workAreas).Height);
        return new RestoredBounds(bar, expandedHeight);
    }

    /// <summary>Pixel height of a collapsed (title-bar-only) zone at the given DPI.</summary>
    public static int CollapsedHeightPx(double collapsedHeightDip, int dpi)
        => (int)Math.Round(collapsedHeightDip * dpi / ZoneLimits.DefaultDpi, MidpointRounding.AwayFromZero);

    public static int DipToPx(double dip, int dpi)
        => (int)Math.Round(dip * dpi / ZoneLimits.DefaultDpi, MidpointRounding.AwayFromZero);

    public static double PxToDip(int px, int dpi)
        => px * (double)ZoneLimits.DefaultDpi / dpi;
}
