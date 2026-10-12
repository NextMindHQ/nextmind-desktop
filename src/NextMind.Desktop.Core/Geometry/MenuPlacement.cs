namespace NextMind.Desktop.Core.Geometry;

public readonly record struct PointPx(int X, int Y);

public readonly record struct SizePx(int Width, int Height);

/// <summary>
/// Chooses where a context menu opens so it does not needlessly cover neighbouring zones. The caller supplies the menu size;
/// candidates start with the normal "below-right of the cursor" spot, so with nothing in the way the menu opens exactly where
/// Windows would put it.
/// </summary>
public static class MenuPlacement
{
    /// <summary>Candidate rectangles in order of preference: the four cursor corners, then beside/above/below the owning zone.</summary>
    public static IReadOnlyList<RectPx> Candidates(PointPx cursor, SizePx menu, RectPx ownZone)
    {
        var (w, h) = (menu.Width, menu.Height);
        return
        [
            new RectPx(cursor.X, cursor.Y, w, h),
            new RectPx(cursor.X - w, cursor.Y, w, h),
            new RectPx(cursor.X, cursor.Y - h, w, h),
            new RectPx(cursor.X - w, cursor.Y - h, w, h),
            new RectPx(ownZone.Right, cursor.Y, w, h),
            new RectPx(ownZone.X - w, cursor.Y, w, h),
            new RectPx(cursor.X, ownZone.Bottom, w, h),
            new RectPx(cursor.X, ownZone.Y - h, w, h),
        ];
    }

    /// <summary>
    /// Candidate indexes, best first: least area outside the work area, then least area over other zones, then the caller's order.
    /// Never empty-handed: every candidate is returned exactly once.
    /// </summary>
    public static IReadOnlyList<int> Rank(IReadOnlyList<RectPx> candidates, IReadOnlyList<RectPx> workAreas, IReadOnlyList<RectPx> otherZones)
    {
        return Enumerable.Range(0, candidates.Count)
            .OrderBy(i => Outside(candidates[i], workAreas))
            .ThenBy(i => otherZones.Sum(z => candidates[i].IntersectionArea(z)))
            .ThenBy(i => i)
            .ToList();
    }

    private static long Outside(RectPx r, IReadOnlyList<RectPx> workAreas)
    {
        if (workAreas.Count == 0)
        {
            return 0;
        }

        return (long)r.Width * r.Height - workAreas.Max(a => r.IntersectionArea(a));
    }
}
