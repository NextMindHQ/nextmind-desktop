using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Tests;

/// <summary>
/// Restoring a zone at startup. A collapsed zone is only its title bar, so only the title bar has to fit on the screen: the saved
/// (expanded) height must not push it upward. The remembered expanded height is kept for the next expand.
/// </summary>
public class ZoneRestoreTests
{
    private const int Strip = 32;
    private static readonly RectPx Primary = new(0, 0, 1920, 1040);
    private static readonly RectPx Right = new(1920, 0, 1920, 1040);
    private static readonly RectPx Small = new(0, 0, 1366, 728);

    [Fact]
    public void CollapsedNearTheBottom_KeepsItsPosition_AndTheExpandedHeight()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(500, 996, 320, 220), collapsed: true, Strip, [Primary]);

        Assert.Equal(new RectPx(500, 996, 320, Strip), r.Visible); // the bug: this used to become y = 820
        Assert.Equal(220, r.ExpandedHeight);
    }

    [Fact]
    public void CollapsedBarPartlyBelowTheScreen_IsPulledUpJustEnough()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(500, 1030, 320, 220), collapsed: true, Strip, [Primary]);
        Assert.Equal(new RectPx(500, 1040 - Strip, 320, Strip), r.Visible);
    }

    [Fact]
    public void Expanded_IsRestoredExactlyAsBefore()
    {
        foreach (var saved in new[]
        {
            new RectPx(500, 996, 320, 220),
            new RectPx(100, 100, 320, 220),
            new RectPx(1800, 1000, 320, 220),
            new RectPx(-1500, 200, 320, 220),
            new RectPx(0, 0, 5000, 4000),
        })
        {
            var r = ZoneGeometry.RestoreBounds(saved, collapsed: false, Strip, [Primary]);
            var old = ZoneGeometry.ClampToWorkAreas(saved, [Primary]);
            Assert.Equal(old, r.Visible);
            Assert.Equal(old.Height, r.ExpandedHeight);
        }
    }

    [Fact]
    public void CollapsedOnAnUnpluggedMonitor_MovesToTheNearestOne_BarStaysVisible()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(-1500, 996, 320, 220), collapsed: true, Strip, [Primary]);

        Assert.Equal(new RectPx(0, 996, 320, Strip), r.Visible);
        Assert.Equal(220, r.ExpandedHeight);
    }

    [Fact]
    public void LowerResolution_CollapsedBarStaysOnScreen_ExpandedHeightFitsTheNewMonitor()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(500, 996, 320, 900), collapsed: true, Strip, [Small]);

        Assert.Equal(new RectPx(500, 728 - Strip, 320, Strip), r.Visible);
        Assert.Equal(728, r.ExpandedHeight); // remembered height is capped to what the monitor can show, not to the saved position
    }

    [Fact]
    public void CollapsedOnASecondMonitor_StaysThere()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(2500, 996, 320, 220), collapsed: true, Strip, [Primary, Right]);
        Assert.Equal(new RectPx(2500, 996, 320, Strip), r.Visible);
    }

    [Fact]
    public void HigherDpiBar_IsTallerButStillFitsAtTheEdge()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(500, 1000, 320, 220), collapsed: true, collapsedHeightPx: 48, [Primary]);
        Assert.Equal(new RectPx(500, 1040 - 48, 320, 48), r.Visible);
    }

    [Fact]
    public void NoKnownMonitors_ReturnsTheSavedPosition()
    {
        var r = ZoneGeometry.RestoreBounds(new RectPx(500, 996, 320, 220), collapsed: true, Strip, []);
        Assert.Equal(new RectPx(500, 996, 320, Strip), r.Visible);
        Assert.Equal(220, r.ExpandedHeight);
    }

    [Theory]
    [InlineData(500, 996, 320, 220, true)]
    [InlineData(500, 996, 320, 220, false)]
    [InlineData(500, 1030, 320, 220, true)]
    [InlineData(-1500, 996, 320, 220, true)]
    [InlineData(100, 100, 320, 220, true)]
    [InlineData(100, 100, 320, 220, false)]
    public void SaveRestartSave_IsStable(int x, int y, int w, int h, bool collapsed)
    {
        var first = ZoneGeometry.RestoreBounds(new RectPx(x, y, w, h), collapsed, Strip, [Primary]);

        // What the app saves while collapsed: the live position and width, and the REMEMBERED expanded height.
        var saved = new RectPx(first.Visible.X, first.Visible.Y, first.Visible.Width, first.ExpandedHeight);
        var second = ZoneGeometry.RestoreBounds(saved, collapsed, Strip, [Primary]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EachZone_IsRestoredFromItsOwnDataOnly()
    {
        var zones = new[]
        {
            (Rect: new RectPx(500, 996, 320, 220), Collapsed: true),
            (Rect: new RectPx(900, 300, 320, 220), Collapsed: false),
            (Rect: new RectPx(100, 1020, 320, 220), Collapsed: true),
        };

        var together = zones.Select(z => ZoneGeometry.RestoreBounds(z.Rect, z.Collapsed, Strip, [Primary])).ToList();
        for (var i = 0; i < zones.Length; i++)
        {
            Assert.Equal(ZoneGeometry.RestoreBounds(zones[i].Rect, zones[i].Collapsed, Strip, [Primary]), together[i]);
        }

        Assert.Equal(new RectPx(900, 300, 320, 220), together[1].Visible); // an untouched, valid zone keeps its saved rectangle
    }
}
