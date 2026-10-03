using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Tests;

public class ZoneGeometryTests
{
    private static readonly RectPx Primary = new(0, 0, 1920, 1040);
    private static readonly RectPx Left = new(-1920, 0, 1920, 1040);

    [Fact]
    public void InsideWorkArea_IsUnchanged()
    {
        var r = new RectPx(100, 100, 320, 220);
        Assert.Equal(r, ZoneGeometry.ClampToWorkAreas(r, [Primary]));
    }

    [Fact]
    public void PartlyOffScreen_IsPulledBackInside()
    {
        var r = ZoneGeometry.ClampToWorkAreas(new RectPx(1800, 1000, 320, 220), [Primary]);
        Assert.Equal(new RectPx(1600, 820, 320, 220), r);
    }

    [Fact]
    public void LargerThanWorkArea_IsShrunk()
    {
        var r = ZoneGeometry.ClampToWorkAreas(new RectPx(0, 0, 5000, 4000), [Primary]);
        Assert.Equal(Primary, r);
    }

    [Fact]
    public void OnUnpluggedMonitor_MovesToNearestRemainingOne()
    {
        // Saved on a monitor to the left of primary that no longer exists.
        var r = ZoneGeometry.ClampToWorkAreas(new RectPx(-1500, 200, 320, 220), [Primary]);
        Assert.Equal(new RectPx(0, 200, 320, 220), r);
    }

    [Fact]
    public void NegativeCoordinates_OnLeftMonitor_AreKept()
    {
        var r = new RectPx(-1700, 100, 320, 220);
        Assert.Equal(r, ZoneGeometry.ClampToWorkAreas(r, [Left, Primary]));
    }

    [Fact]
    public void StraddlingTwoMonitors_PicksTheOneWithMostOverlap()
    {
        var r = ZoneGeometry.ClampToWorkAreas(new RectPx(-100, 100, 320, 220), [Left, Primary]);
        Assert.Equal(new RectPx(0, 100, 320, 220), r); // 220 px of the width are on primary, 100 on the left one
    }

    [Fact]
    public void NoWorkAreas_ReturnsInputUnchanged()
    {
        var r = new RectPx(5, 6, 7, 8);
        Assert.Equal(r, ZoneGeometry.ClampToWorkAreas(r, []));
    }

    [Theory]
    [InlineData(32, 96, 32)]
    [InlineData(32, 120, 40)]
    [InlineData(32, 144, 48)]
    [InlineData(32, 192, 64)]
    public void CollapsedHeight_ScalesWithDpi(double dip, int dpi, int expectedPx)
        => Assert.Equal(expectedPx, ZoneGeometry.CollapsedHeightPx(dip, dpi));

    [Fact]
    public void PxDip_Conversions_AreInverse()
    {
        Assert.Equal(300, ZoneGeometry.DipToPx(ZoneGeometry.PxToDip(300, 144), 144));
    }

    [Fact]
    public void FirstZone_IsPlacedInsideThePrimaryWorkArea_WithTheSharkTitle()
    {
        var z = ZoneFactory.Create(ZoneFactory.FirstZoneTitle, Primary, 144);

        Assert.Equal("🦈 NextMind", z.Title);
        Assert.Equal(ZoneGeometry.DipToPx(320, 144), z.Width);
        Assert.Equal(144, z.Dpi);
        Assert.Equal(z.Width, ZoneGeometry.ClampToWorkAreas(new RectPx(z.X, z.Y, z.Width, z.Height), [Primary]).Width);
        Assert.InRange(z.X, Primary.X, Primary.Right - z.Width);
        Assert.InRange(z.Y, Primary.Y, Primary.Bottom - z.Height);
        Assert.False(z.Collapsed);
    }

    [Fact]
    public void FurtherZones_Cascade()
    {
        var a = ZoneFactory.Create("a", Primary, 96, 0);
        var b = ZoneFactory.Create("b", Primary, 96, 1);
        Assert.NotEqual((a.X, a.Y), (b.X, b.Y));
    }
}
