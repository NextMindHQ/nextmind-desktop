using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Core.Layout;

public sealed record LayoutOptions(
    bool SnapToElements,
    bool SnapToGrid,
    int GridSize,
    int Gap,
    int SnapThreshold)
{
    public static LayoutOptions Default { get; } = new(true, false, 16, 12, 10);
}

public readonly record struct LayoutElement(string Id, RectPx Bounds);

public sealed record LayoutResult(RectPx Bounds, IReadOnlyList<string> CollidingIds)
{
    public bool HasCollision => CollidingIds.Count > 0;
}

public static class LayoutInteraction
{
    public static bool ShouldApplyNativeElementSnap(LayoutOptions options, bool altBypass)
        => options.SnapToElements && !altBypass;

    public static LayoutOptions AfterNativeMoveOptions(LayoutOptions options, bool altBypass)
        => options with
        {
            // Element snapping is performed once, by the native WM_MOVING path where DPI and work areas are available.
            SnapToElements = false,
            SnapToGrid = options.SnapToGrid && !altBypass,
        };
}

public static class DesktopLayoutEngine
{
    public static LayoutResult Place(RectPx proposed, IReadOnlyList<LayoutElement> peers, LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(peers);
        ArgumentNullException.ThrowIfNull(options);

        var x = options.SnapToGrid ? SnapToGrid(proposed.X, options.GridSize) : proposed.X;
        var y = options.SnapToGrid ? SnapToGrid(proposed.Y, options.GridSize) : proposed.Y;

        if (options.SnapToElements && peers.Count > 0)
        {
            x = NearestWithin(
                x,
                peers.SelectMany(peer => HorizontalCandidates(peer.Bounds, proposed.Width, options.Gap)),
                options.SnapThreshold);
            y = NearestWithin(
                y,
                peers.SelectMany(peer => VerticalCandidates(peer.Bounds, proposed.Height, options.Gap)),
                options.SnapThreshold);
        }

        var bounds = new RectPx(x, y, proposed.Width, proposed.Height);
        var collisions = peers
            .Where(peer => bounds.IntersectionArea(peer.Bounds) > 0)
            .Select(peer => peer.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return new LayoutResult(bounds, collisions);
    }

    public static IReadOnlyDictionary<string, RectPx> AutoArrange(
        IReadOnlyList<LayoutElement> elements,
        IReadOnlyList<RectPx> workAreas,
        LayoutOptions options)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(workAreas);
        ArgumentNullException.ThrowIfNull(options);

        var result = new Dictionary<string, RectPx>(StringComparer.OrdinalIgnoreCase);
        if (workAreas.Count == 0)
        {
            foreach (var element in elements)
            {
                result[element.Id] = element.Bounds;
            }

            return result;
        }

        var ordered = elements.OrderBy(element => element.Id, StringComparer.Ordinal).ToList();
        var areaIndex = 0;
        var area = workAreas[areaIndex];
        var x = area.X + options.Gap;
        var y = area.Y + options.Gap;
        var rowHeight = 0;

        foreach (var element in ordered)
        {
            var bounds = element.Bounds;
            if (x + bounds.Width + options.Gap > area.Right && x > area.X + options.Gap)
            {
                x = area.X + options.Gap;
                y += rowHeight + options.Gap;
                rowHeight = 0;
            }

            if (y + bounds.Height + options.Gap > area.Bottom && areaIndex + 1 < workAreas.Count)
            {
                area = workAreas[++areaIndex];
                x = area.X + options.Gap;
                y = area.Y + options.Gap;
                rowHeight = 0;
            }

            var placed = ZoneGeometry.ClampToWorkAreas(
                new RectPx(x, y, bounds.Width, bounds.Height),
                [area]);
            result[element.Id] = placed;
            x = placed.Right + options.Gap;
            rowHeight = Math.Max(rowHeight, placed.Height);
        }

        return result;
    }

    private static int SnapToGrid(int value, int size)
    {
        if (size <= 1)
        {
            return value;
        }

        return (int)Math.Round(value / (double)size, MidpointRounding.AwayFromZero) * size;
    }

    private static int NearestWithin(int value, IEnumerable<int> candidates, int threshold)
    {
        var best = value;
        var bestDistance = Math.Max(0, threshold) + 1;
        foreach (var candidate in candidates)
        {
            var distance = Math.Abs((long)candidate - value);
            if (distance < bestDistance || (distance == bestDistance && candidate < best))
            {
                best = candidate;
                bestDistance = distance > int.MaxValue ? int.MaxValue : (int)distance;
            }
        }

        return bestDistance <= Math.Max(0, threshold) ? best : value;
    }

    private static IEnumerable<int> HorizontalCandidates(RectPx peer, int width, int gap)
    {
        yield return peer.X;
        yield return peer.Right - width;
        yield return peer.Right + gap;
        yield return peer.X - gap - width;
    }

    private static IEnumerable<int> VerticalCandidates(RectPx peer, int height, int gap)
    {
        yield return peer.Y;
        yield return peer.Bottom - height;
        yield return peer.Bottom + gap;
        yield return peer.Y - gap - height;
    }
}
