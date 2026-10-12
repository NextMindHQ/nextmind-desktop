using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Tests;

public class SubmenuPlacementTests
{
    private static readonly RectPx Work = new(0, 0, 1920, 1040);
    private static readonly SizePx Sub = new(160, 150);

    private static RectPx Best(RectPx parent, IReadOnlyList<RectPx> zones)
    {
        var candidates = MenuPlacement.SubmenuCandidates(parent, Sub);
        return candidates[MenuPlacement.Rank(candidates, [Work], zones)[0]];
    }

    [Fact]
    public void Candidates_StartWithTheNormalPlacement_RightOfTheItem_TopAligned()
    {
        var parent = new RectPx(500, 600, 180, 24);
        var c = MenuPlacement.SubmenuCandidates(parent, Sub);

        Assert.Equal(new RectPx(680, 600, 160, 150), c[0]);
        Assert.Equal(new RectPx(340, 600, 160, 150), c[1]); // left, top-aligned
        Assert.Equal(new RectPx(680, 474, 160, 150), c[2]); // right, bottom-aligned (bottom edge = item bottom)
        Assert.Equal(new RectPx(340, 474, 160, 150), c[3]);
    }

    [Fact]
    public void Candidates_NeverCoverTheParentItem()
    {
        var parent = new RectPx(500, 600, 180, 24);
        foreach (var c in MenuPlacement.SubmenuCandidates(parent, Sub))
        {
            Assert.Equal(0, c.IntersectionArea(parent));
        }
    }

    [Fact]
    public void FreeSpace_KeepsTheNormalRightPlacement()
        => Assert.Equal(new RectPx(680, 600, 160, 150), Best(new RectPx(500, 600, 180, 24), []));

    [Fact]
    public void NeighbourToTheRight_MovesTheSubmenuToTheLeft()
    {
        var neighbour = new RectPx(700, 560, 320, 220);
        var best = Best(new RectPx(500, 600, 180, 24), [neighbour]);

        Assert.Equal(0, best.IntersectionArea(neighbour));
        Assert.True(best.Right <= 500, "opens to the left of the item");
    }

    [Fact]
    public void NeighboursOnBothSides_ChoosesTheSmallestCover()
    {
        var big = new RectPx(680, 500, 400, 400);   // right: large
        var small = new RectPx(100, 640, 320, 30);  // left: small
        var best = Best(new RectPx(500, 600, 180, 24), [big, small]);

        var cover = best.IntersectionArea(big) + best.IntersectionArea(small);
        var rightCover = new RectPx(680, 600, 160, 150).IntersectionArea(big) + new RectPx(680, 600, 160, 150).IntersectionArea(small);
        Assert.True(cover < rightCover);
    }

    [Fact]
    public void NearTheBottomEdge_AlignsTheSubmenuToTheBottomOfTheItem()
    {
        var best = Best(new RectPx(500, 1010, 180, 24), []);

        Assert.True(best.Bottom <= Work.Bottom);
        Assert.Equal(680, best.X);
        Assert.Equal(1034 - 150, best.Y);
    }

    [Fact]
    public void NearTheRightEdge_OpensToTheLeft()
    {
        var best = Best(new RectPx(1740, 600, 180, 24), []);
        Assert.True(best.Right <= 1740 && best.X >= 0);
    }

    [Fact]
    public void BottomRightCorner_OpensLeftAndUp()
    {
        var best = Best(new RectPx(1740, 1010, 180, 24), []);
        Assert.True(best.Right <= 1740 && best.Bottom <= Work.Bottom);
    }

    [Fact]
    public void WhenEverythingIsCovered_StillReturnsAPlacementInsideTheScreen()
    {
        var all = new RectPx(0, 0, 1920, 1040);
        var best = Best(new RectPx(900, 500, 180, 24), [all]);

        Assert.Equal(0, new RectPx(0, 0, 1920, 1040).IntersectionArea(best) - (long)best.Width * best.Height);
        Assert.True(best.X >= 0 && best.Y >= 0 && best.Right <= Work.Right && best.Bottom <= Work.Bottom);
    }

    // ---------- popup offset (the template adds it after the placement callback) ----------

    private static readonly PointPx TemplateOffset = new(-2, -3);

    [Fact]
    public void Offset_IsAppliedToEveryCandidate_SoTheyDescribeWhereTheSubmenuReallyAppears()
    {
        var parent = new RectPx(500, 600, 180, 24);
        var plain = MenuPlacement.SubmenuCandidates(parent, Sub);
        var shifted = MenuPlacement.SubmenuCandidates(parent, Sub, TemplateOffset);

        for (var i = 0; i < plain.Count; i++)
        {
            Assert.Equal(plain[i].X - 2, shifted[i].X);
            Assert.Equal(plain[i].Y - 3, shifted[i].Y);
        }
    }

    [Fact]
    public void Ranking_UsesTheTrueRectangle_NotTheRawOne()
    {
        var parent = new RectPx(500, 600, 180, 24);
        // A zone that ends 2 px below where the normal right-hand submenu would start: only the template offset makes them touch.
        var zone = new RectPx(680, 540, 200, 58);

        var raw = MenuPlacement.SubmenuCandidates(parent, Sub, default, [Work]);
        var rawBest = raw[MenuPlacement.Rank(raw, [Work], [zone])[0]];
        Assert.Equal(raw[0], rawBest); // without the offset the normal placement looks free

        var real = MenuPlacement.SubmenuCandidates(parent, Sub, TemplateOffset, [Work]);
        var best = real[MenuPlacement.Rank(real, [Work], [zone])[0]];
        Assert.Equal(0, best.IntersectionArea(zone)); // with it, the zone is covered, so another spot wins
        Assert.NotEqual(real[0], best);
    }

    // ---------- long lists ("Move to Zone" with many zones) ----------

    [Fact]
    public void TallSubmenu_NearTheBottom_IsPulledIntoTheMonitor()
    {
        var tall = new SizePx(160, 900);
        var parent = new RectPx(500, 800, 180, 24);
        var candidates = MenuPlacement.SubmenuCandidates(parent, tall, TemplateOffset, [Work]);
        var best = candidates[MenuPlacement.Rank(candidates, [Work], [])[0]];

        Assert.True(best.Y >= Work.Y && best.Bottom <= Work.Bottom, $"{best}");
    }

    [Fact]
    public void SubmenuTallerThanTheMonitor_StartsAtTheTop_ForTheLeastOutside()
    {
        var huge = new SizePx(160, 1200);
        var parent = new RectPx(500, 500, 180, 24);
        var candidates = MenuPlacement.SubmenuCandidates(parent, huge, TemplateOffset, [Work]);
        var best = candidates[MenuPlacement.Rank(candidates, [Work], [])[0]];

        Assert.Equal(Work.Y, best.Y);
    }

    [Fact]
    public void TallSubmenu_StillAvoidsANeighbour_WhenThereIsRoom()
    {
        var tall = new SizePx(160, 900);
        var parent = new RectPx(500, 400, 180, 24);
        var neighbour = new RectPx(700, 0, 400, 1040); // the whole right-hand column
        var candidates = MenuPlacement.SubmenuCandidates(parent, tall, TemplateOffset, [Work]);
        var best = candidates[MenuPlacement.Rank(candidates, [Work], [neighbour])[0]];

        Assert.Equal(0, best.IntersectionArea(neighbour));
        Assert.True(best.Right <= 500);
    }

    [Fact]
    public void Ranking_IsStable_ForEqualScores()
    {
        var parent = new RectPx(500, 600, 180, 24);
        var candidates = MenuPlacement.SubmenuCandidates(parent, Sub);
        var a = MenuPlacement.Rank(candidates, [Work], []);
        var b = MenuPlacement.Rank(candidates, [Work], []);
        Assert.Equal(a, b);
        Assert.Equal(0, a[0]);
    }
}

/// <summary>
/// Static guards for the submenu placement: it must stay on the public template contract (PART_Popup by name, no reflection), cover both the
/// static "Sort by" and the per-open "Move to Zone", keep a safe fallback and stay quiet in production.
/// </summary>
public class SubmenuPlacementWiringTests
{
    private static string Source()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NextMind.Desktop.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repo root not found."),
            "src", "NextMind.Desktop.App", "ZoneWindow.xaml.cs"));
    }

    private static string Body(string source, string signature)
    {
        var match = System.Text.RegularExpressions.Regex.Match(source, System.Text.RegularExpressions.Regex.Escape(signature) + @"[\s\S]*?\n    \}");
        Assert.True(match.Success, "method not found: " + signature);
        return match.Value;
    }

    [Fact]
    public void BothSubmenus_AreRegistered_TheStaticOne_AndTheOneRebuiltOnEveryOpen()
    {
        var src = Source();
        Assert.Contains("UseSmartSubmenu(SortMenu);", src);
        Assert.Matches(@"var parent = new MenuItem \{ Header = ""Move to Zone"" \};\s*UseSmartSubmenu\(parent\);", src);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(src, @"UseSmartSubmenu\((?!MenuItem item)").Count);
    }

    [Fact]
    public void Popups_AreTrackedPerPopup_NotInASingleField()
    {
        var src = Source();
        Assert.Contains("ConditionalWeakTable<Popup, object>", src);
        Assert.DoesNotContain("_hookedSubmenuPopup", src);
    }

    [Fact]
    public void OnlyThePublicTemplateContractIsUsed_NoReflection()
    {
        var src = Source();
        Assert.Contains("FindName(\"PART_Popup\"", src);
        foreach (var forbidden in new[] { "BindingFlags", "GetField(", "GetProperty(", "System.Reflection", "typeof(MenuItem).Get" })
        {
            Assert.DoesNotContain(forbidden, src);
        }
    }

    [Fact]
    public void Placement_FallsBackToTheDefaultSpot_OnAnyFailure()
    {
        var body = Body(Source(), "private CustomPopupPlacement[] PlaceSubmenu(");
        Assert.Contains("catch (Exception", body);
        Assert.Matches(@"catch \(Exception ex\)[\s\S]*DefaultSubmenuPlacement\(item\)", body);

        var attach = Body(Source(), "private void AttachSubmenuPlacement(");
        Assert.Contains("is not Popup popup", attach);
        Assert.Contains("the default placement stays", attach);
    }

    [Fact]
    public void ProductionStaysQuiet_TracingIsDevelopmentOnly_AndNotInTheCallbackHotPath()
    {
        var src = Source();
        Assert.Matches(@"TraceSubmenus\s*=\s*!string\.IsNullOrEmpty\(Environment\.GetEnvironmentVariable\(AppPaths\.ConfigDirectoryOverrideVariable\)\)", src);
        Assert.Matches(@"if \(TraceSubmenus\)\s*\{\s*TraceSubmenuPlacement\(", src);
        Assert.DoesNotContain("_log.Info(", Body(src, "private CustomPopupPlacement[] PlaceSubmenu("));
    }
}