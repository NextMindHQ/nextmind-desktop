using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Core.Layout;

namespace NextMind.Desktop.Tests;

public sealed class DesktopLayoutEngineTests
{
    [Fact]
    public void Place_SnapsNearestPeerEdges_WithConfiguredGap()
    {
        var proposed = new RectPx(287, 100, 100, 80);
        var peers = new[] { new LayoutElement("peer", new RectPx(100, 100, 180, 120)) };

        var result = DesktopLayoutEngine.Place(
            proposed,
            peers,
            new LayoutOptions(SnapToElements: true, SnapToGrid: false, GridSize: 20, Gap: 8, SnapThreshold: 12));

        Assert.Equal(new RectPx(288, 100, 100, 80), result.Bounds);
        Assert.False(result.HasCollision);
    }

    [Fact]
    public void Place_DoesNotApplyGrid_WhenGridSnapIsDisabled()
    {
        var proposed = new RectPx(113, 127, 100, 80);

        var result = DesktopLayoutEngine.Place(
            proposed,
            [],
            new LayoutOptions(SnapToElements: false, SnapToGrid: false, GridSize: 20, Gap: 8, SnapThreshold: 12));

        Assert.Equal(proposed, result.Bounds);
    }

    [Fact]
    public void Place_AppliesOptionalGrid_ToPositionOnly()
    {
        var proposed = new RectPx(113, -127, 101, 79);

        var result = DesktopLayoutEngine.Place(
            proposed,
            [],
            new LayoutOptions(SnapToElements: false, SnapToGrid: true, GridSize: 20, Gap: 8, SnapThreshold: 12));

        Assert.Equal(new RectPx(120, -120, 101, 79), result.Bounds);
    }

    [Fact]
    public void Place_ReportsButDoesNotBlockOverlap()
    {
        var proposed = new RectPx(150, 150, 100, 100);
        var peers = new[]
        {
            new LayoutElement("under", new RectPx(100, 100, 100, 100)),
            new LayoutElement("away", new RectPx(500, 500, 20, 20)),
        };

        var result = DesktopLayoutEngine.Place(proposed, peers, LayoutOptions.Default with { SnapToElements = false });

        Assert.Equal(proposed, result.Bounds);
        Assert.True(result.HasCollision);
        Assert.Equal(new[] { "under" }, result.CollidingIds);
    }

    [Fact]
    public void AutoArrange_IsDeterministic_AndSupportsNegativeMonitorCoordinates()
    {
        var elements = new[]
        {
            new LayoutElement("b", new RectPx(999, 999, 100, 80)),
            new LayoutElement("a", new RectPx(999, 999, 120, 90)),
            new LayoutElement("c", new RectPx(999, 999, 80, 70)),
        };
        var workAreas = new[] { new RectPx(-400, 0, 400, 300) };
        var options = LayoutOptions.Default with { Gap = 10 };

        var arranged = DesktopLayoutEngine.AutoArrange(elements, workAreas, options);

        Assert.Equal(new RectPx(-390, 10, 120, 90), arranged["a"]);
        Assert.Equal(new RectPx(-260, 10, 100, 80), arranged["b"]);
        Assert.Equal(new RectPx(-150, 10, 80, 70), arranged["c"]);
        Assert.Equal(arranged, DesktopLayoutEngine.AutoArrange(elements, workAreas, options));
    }

    [Fact]
    public void UndoState_TakesOnlyMostRecentMoveOnce()
    {
        var undo = new LayoutUndoState();
        var move = new LayoutMove("zone-1", new RectPx(10, 20, 100, 80), new RectPx(30, 40, 100, 80));

        undo.Record(move);

        Assert.Equal(move, undo.Take());
        Assert.Null(undo.Take());
    }
}
