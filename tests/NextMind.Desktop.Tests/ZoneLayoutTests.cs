using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Tests;

public class ZoneLayoutTests
{
    private static readonly RectPx Work = new(0, 0, 1920, 1040);
    private static readonly RectPx[] Works = [Work];

    // ---------- free slot for a new zone ----------

    [Fact]
    public void FreeSlot_EmptyDesktop_UsesTopRightCorner()
    {
        var slot = ZoneLayout.FindFreeSlot(320, 220, Work, [], margin: 48, gap: 12, step: 24);
        Assert.Equal(new RectPx(1920 - 320 - 48, 48, 320, 220), slot);
    }

    [Fact]
    public void FreeSlot_SkipsOccupiedArea()
    {
        var first = new RectPx(1920 - 320 - 48, 48, 320, 220);
        var slot = ZoneLayout.FindFreeSlot(320, 220, Work, [first], margin: 48, gap: 12, step: 24);

        Assert.NotNull(slot);
        Assert.Equal(0, slot!.Value.IntersectionArea(first));
        Assert.True(Overlaps(slot.Value, first, 12) == false, "must keep the gap");
    }

    [Fact]
    public void FreeSlot_ManyZones_NeverOverlapsAndStaysInsideWorkArea()
    {
        var placed = new List<RectPx>();
        for (var i = 0; i < 12; i++)
        {
            var slot = ZoneLayout.FindFreeSlot(320, 220, Work, placed, margin: 48, gap: 12, step: 24);
            Assert.NotNull(slot);
            Assert.All(placed, p => Assert.Equal(0, p.IntersectionArea(slot!.Value)));
            Assert.True(slot!.Value.X >= 0 && slot.Value.Y >= 0 && slot.Value.Right <= Work.Right && slot.Value.Bottom <= Work.Bottom);
            placed.Add(slot.Value);
        }
    }

    [Fact]
    public void FreeSlot_NoRoom_ReturnsNull()
    {
        var full = new RectPx(0, 0, 1920, 1040);
        Assert.Null(ZoneLayout.FindFreeSlot(320, 220, Work, [full], margin: 48, gap: 12, step: 24));
    }

    [Fact]
    public void AddZone_WithOccupied_PlacesInFreeSlot_AndWithoutKeepsLegacyCascade()
    {
        var first = new ZoneConfig { X = 1552, Y = 48, Width = 320, Height = 220 };
        var cfg = new AppConfig();
        var zone = ZoneCatalog.AddZone(cfg, "B", Work, 96, [new RectPx(first.X, first.Y, first.Width, first.Height)]);
        Assert.Equal(0, new RectPx(zone.X, zone.Y, zone.Width, zone.Height).IntersectionArea(new RectPx(first.X, first.Y, first.Width, first.Height)));

        var legacy = ZoneCatalog.AddZone(new AppConfig(), "A", Work, 96);
        Assert.Equal(1552, legacy.X);
        Assert.Equal(48, legacy.Y);
    }

    // ---------- snapping while dragging ----------

    [Fact]
    public void Snap_NearRightEdgeOfNeighbour_AbutsWithGap()
    {
        var other = new RectPx(100, 100, 300, 200);
        var moving = new RectPx(412 + 5, 110, 300, 200); // 5 px away from "other.Right + gap(12)"
        var r = ZoneLayout.Snap(moving, [other], Works, threshold: 12, gap: 12);
        Assert.Equal(412, r.X);
    }

    [Fact]
    public void Snap_AlignsTopEdgesOfNeighbours()
    {
        var other = new RectPx(100, 100, 300, 200);
        var moving = new RectPx(600, 106, 300, 200);
        var r = ZoneLayout.Snap(moving, [other], Works, threshold: 12, gap: 12);
        Assert.Equal(100, r.Y);
        Assert.Equal(600, r.X); // nothing to snap horizontally
    }

    [Fact]
    public void Snap_FarAway_IsUnchanged()
    {
        var other = new RectPx(100, 100, 300, 200);
        var moving = new RectPx(900, 600, 300, 200);
        Assert.Equal(moving, ZoneLayout.Snap(moving, [other], Works, threshold: 12, gap: 12));
    }

    [Fact]
    public void Snap_DeliberateOverlap_IsNotPulledApart()
    {
        var other = new RectPx(100, 100, 300, 200);
        var moving = new RectPx(220, 150, 300, 200); // deep inside the neighbour, no edge within the threshold
        Assert.Equal(moving, ZoneLayout.Snap(moving, [other], Works, threshold: 12, gap: 12));
    }

    [Fact]
    public void Snap_SnapsToWorkAreaEdge_Flush()
    {
        var moving = new RectPx(7, 500, 300, 200);
        Assert.Equal(0, ZoneLayout.Snap(moving, [], Works, threshold: 12, gap: 12).X);
    }

    [Fact]
    public void Snap_KeepsSize()
    {
        var r = ZoneLayout.Snap(new RectPx(417, 110, 300, 200), [new RectPx(100, 100, 300, 200)], Works, 12, 12);
        Assert.Equal(300, r.Width);
        Assert.Equal(200, r.Height);
    }

    // ---------- expanding ----------

    [Fact]
    public void Expand_FreeSpace_GrowsDownAndKeepsPosition()
    {
        var collapsed = new RectPx(500, 100, 320, 32);
        var r = ZoneLayout.PlanExpand(collapsed, 220, [], Works);
        Assert.Equal(new RectPx(500, 100, 320, 220), r);
    }

    [Fact]
    public void Expand_NeighbourBelow_GrowsUpInstead()
    {
        var collapsed = new RectPx(500, 600, 320, 32);
        var below = new RectPx(480, 660, 400, 300);
        var r = ZoneLayout.PlanExpand(collapsed, 220, [below], Works);

        Assert.Equal(0, r.IntersectionArea(below));
        Assert.Equal(collapsed.Bottom, r.Bottom); // bottom edge stays, the zone opens upward
        Assert.Equal(220, r.Height);
    }

    [Fact]
    public void Expand_NoDirectionIsFree_PicksTheLeastOverlap_NeverMovesOthers()
    {
        var collapsed = new RectPx(500, 500, 320, 32);
        var big = new RectPx(480, 540, 400, 400);   // below, large
        var small = new RectPx(480, 400, 400, 60);  // above, small
        var r = ZoneLayout.PlanExpand(collapsed, 220, [big, small], Works);

        var down = new RectPx(500, 500, 320, 220);
        Assert.True(r.IntersectionArea(big) + r.IntersectionArea(small) <= down.IntersectionArea(big) + down.IntersectionArea(small));
    }

    [Fact]
    public void Expand_NearBottomEdge_StaysInsideWorkArea()
    {
        var collapsed = new RectPx(500, 1000, 320, 32);
        var r = ZoneLayout.PlanExpand(collapsed, 220, [], Works);
        Assert.True(r.Bottom <= Work.Bottom && r.Y >= 0);
        Assert.Equal(220, r.Height);
    }

    [Fact]
    public void Expand_TieBetweenDirections_PrefersDown()
    {
        // Same overlap either way: down must win so the behaviour stays predictable.
        var collapsed = new RectPx(500, 500, 320, 32);
        var r = ZoneLayout.PlanExpand(collapsed, 220, [new RectPx(0, 0, 10, 10)], Works);
        Assert.Equal(500, r.Y);
    }

    private static bool Overlaps(RectPx a, RectPx b, int gap)
        => a.X < b.Right + gap && a.Right + gap > b.X && a.Y < b.Bottom + gap && a.Bottom + gap > b.Y;
}

public class MenuPlacementTests
{
    private static readonly RectPx Work = new(0, 0, 1920, 1040);

    [Fact]
    public void Candidates_StartWithTheNormalPlacementAtTheCursor()
    {
        var own = new RectPx(500, 500, 320, 220);
        var c = MenuPlacement.Candidates(new PointPx(600, 520), new SizePx(180, 200), own);
        Assert.Equal(new RectPx(600, 520, 180, 200), c[0]);
    }

    [Fact]
    public void Rank_NoNeighbours_KeepsPreferredOrder()
    {
        var own = new RectPx(500, 500, 320, 220);
        var c = MenuPlacement.Candidates(new PointPx(600, 520), new SizePx(180, 200), own);
        var order = MenuPlacement.Rank(c, [Work], []);
        Assert.Equal(0, order[0]);
    }

    [Fact]
    public void Rank_MenuAtCursorWouldCoverNeighbour_PrefersAPlaceThatDoesNot()
    {
        var own = new RectPx(500, 500, 320, 220);
        var work = new RectPx(900, 480, 320, 220); // right next to "own", where the default menu would land
        var c = MenuPlacement.Candidates(new PointPx(780, 520), new SizePx(180, 200), own);
        var best = c[MenuPlacement.Rank(c, [Work], [work])[0]];
        Assert.Equal(0, best.IntersectionArea(work));
    }

    [Fact]
    public void Rank_PenalisesPlacementsOutsideTheWorkArea()
    {
        var own = new RectPx(1700, 900, 200, 120);
        var c = MenuPlacement.Candidates(new PointPx(1850, 980), new SizePx(180, 200), own);
        var best = c[MenuPlacement.Rank(c, [Work], [])[0]];
        Assert.True(best.Right <= Work.Right && best.Bottom <= Work.Bottom && best.X >= 0 && best.Y >= 0);
    }

    [Fact]
    public void Rank_ReturnsEveryCandidateExactlyOnce()
    {
        var own = new RectPx(500, 500, 320, 220);
        var c = MenuPlacement.Candidates(new PointPx(600, 520), new SizePx(180, 200), own);
        var order = MenuPlacement.Rank(c, [Work], [new RectPx(0, 0, 3000, 3000)]);
        Assert.Equal(c.Count, order.Distinct().Count());
    }
}
