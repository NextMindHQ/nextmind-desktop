namespace NextMind.Desktop.Core.Geometry;

/// <summary>
/// Pure layout helpers (physical pixels). Nothing here moves a zone on its own: they only answer "where would a good spot be?"
/// so the UI can offer it. The user stays in control — overlapping zones are always allowed.
/// </summary>
public static class ZoneLayout
{
    /// <summary>
    /// First spot for a new zone that overlaps no <paramref name="occupied"/> rectangle (keeping <paramref name="gap"/> clear),
    /// scanning from the top-right corner downward, then column by column to the left. Null when there is no room.
    /// </summary>
    public static RectPx? FindFreeSlot(int width, int height, RectPx workArea, IReadOnlyList<RectPx> occupied, int margin, int gap, int step)
    {
        step = Math.Max(1, step);
        var minX = workArea.X + margin;
        var minY = workArea.Y + margin;
        var maxX = workArea.Right - width - margin;
        var maxY = workArea.Bottom - height - margin;

        for (var x = maxX; x >= minX; x -= step)
        {
            for (var y = minY; y <= maxY; y += step)
            {
                var candidate = new RectPx(x, y, width, height);
                if (!occupied.Any(o => TooClose(candidate, o, gap)))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Magnetic alignment for a zone being dragged: edges snap to a neighbour's edge (aligned) or next to it (abutting, with
    /// <paramref name="gap"/>), and to the work-area edges, but only within <paramref name="threshold"/> pixels. Deliberate overlap is
    /// never undone: a zone dragged deep onto another has no edge in range and stays exactly where the user put it. Size is unchanged.
    /// </summary>
    public static RectPx Snap(RectPx moving, IReadOnlyList<RectPx> others, IReadOnlyList<RectPx> workAreas, int threshold, int gap)
    {
        var dx = BestDelta(Edges(moving, others, workAreas, threshold, gap, horizontal: true), threshold);
        var dy = BestDelta(Edges(moving, others, workAreas, threshold, gap, horizontal: false), threshold);
        return moving with { X = moving.X + dx, Y = moving.Y + dy };
    }

    /// <summary>
    /// Where an expanding zone should open. The zone grows downward from its title bar (the default), or upward with its bottom
    /// edge fixed, whichever covers fewer other zones; both are kept inside the work area. Ties and the free-space case choose
    /// downward, so the usual behaviour is the predictable one. Other zones are never moved.
    /// </summary>
    public static RectPx PlanExpand(RectPx collapsed, int expandedHeight, IReadOnlyList<RectPx> others, IReadOnlyList<RectPx> workAreas)
    {
        var down = new RectPx(collapsed.X, collapsed.Y, collapsed.Width, expandedHeight);
        var up = new RectPx(collapsed.X, collapsed.Bottom - expandedHeight, collapsed.Width, expandedHeight);

        var best = Fit(down);
        var bestScore = Score(best);
        var other = Fit(up);
        if (Score(other).CompareTo(bestScore) < 0)
        {
            best = other;
        }

        return best;

        RectPx Fit(RectPx r) => ZoneGeometry.ClampToWorkAreas(r, workAreas);

        (long Overlap, int Displacement) Score(RectPx r)
            => (others.Sum(o => r.IntersectionArea(o)), Math.Abs(r.Y - collapsed.Y));
    }

    private static bool TooClose(RectPx a, RectPx b, int gap)
        => a.X < b.Right + gap && a.Right + gap > b.X && a.Y < b.Bottom + gap && a.Bottom + gap > b.Y;

    /// <summary>Candidate shifts for one axis. "Aligned" snaps need no proximity; "abutting" snaps need the other axis to be near.</summary>
    private static IEnumerable<int> Edges(RectPx m, IReadOnlyList<RectPx> others, IReadOnlyList<RectPx> workAreas, int threshold, int gap, bool horizontal)
    {
        var (mLow, mHigh) = horizontal ? (m.X, m.Right) : (m.Y, m.Bottom);

        foreach (var area in workAreas)
        {
            var (low, high) = horizontal ? (area.X, area.Right) : (area.Y, area.Bottom);
            yield return low - mLow;
            yield return high - mHigh;
        }

        foreach (var o in others)
        {
            var (oLow, oHigh) = horizontal ? (o.X, o.Right) : (o.Y, o.Bottom);
            yield return oLow - mLow;   // aligned, low edges
            yield return oHigh - mHigh; // aligned, high edges

            var near = horizontal
                ? m.Y < o.Bottom + threshold && m.Bottom > o.Y - threshold
                : m.X < o.Right + threshold && m.Right > o.X - threshold;
            if (near)
            {
                yield return oHigh + gap - mLow;  // abutting after the neighbour
                yield return oLow - gap - mHigh;  // abutting before the neighbour
            }
        }
    }

    private static int BestDelta(IEnumerable<int> deltas, int threshold)
    {
        var best = 0;
        var bestAbs = int.MaxValue;
        foreach (var d in deltas)
        {
            var abs = Math.Abs(d);
            if (abs <= threshold && abs < bestAbs)
            {
                best = d;
                bestAbs = abs;
            }
        }

        return bestAbs == int.MaxValue ? 0 : best;
    }
}
