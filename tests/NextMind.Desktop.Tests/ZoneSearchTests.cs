using System.Diagnostics;
using System.Text.RegularExpressions;
using NextMind.Desktop.Core.Search;

namespace NextMind.Desktop.Tests;

public class ZoneSearchTests
{
    private static IReadOnlyList<string> Keys(params (string Name, string Path)[] items)
        => items.Select(i => ZoneSearch.BuildKey(i.Name, i.Path)).ToList();

    [Theory]
    [InlineData("Zażółć GĘŚLĄ jaźń", "zazolc gesla jazn")]
    [InlineData("ŁÓDŹ", "lodz")]
    [InlineData("Straße", "straße")] // ß has no decomposition; only lower-cased (no German folding is claimed)
    [InlineData("", "")]
    [InlineData("Plain", "plain")]
    public void Normalize_IgnoresCaseAndPolishLetters(string input, string expected)
        => Assert.Equal(expected, ZoneSearch.Normalize(input));

    [Fact]
    public void EmptyOrWhitespaceQuery_ReturnsEveryItem_InTheOriginalOrder()
    {
        var keys = Keys(("b", "C:\\x\\b.txt"), ("a", "C:\\x\\a.txt"), ("c", "C:\\x\\c.txt"));
        Assert.Equal([0, 1, 2], ZoneSearch.Filter(keys, ""));
        Assert.Equal([0, 1, 2], ZoneSearch.Filter(keys, "   "));
        Assert.Equal([0, 1, 2], ZoneSearch.Filter(keys, null));
    }

    [Fact]
    public void Filter_KeepsTheOrderOfTheZone_NotAlphabetical()
    {
        var keys = Keys(("Zebra report", "C:\\z.txt"), ("Alpha report", "C:\\a.txt"), ("Other", "C:\\o.txt"), ("Beta report", "C:\\b.txt"));
        Assert.Equal([0, 1, 3], ZoneSearch.Filter(keys, "report"));
    }

    [Fact]
    public void Matching_IsCaseInsensitive_AndIgnoresPolishDiacritics()
    {
        var keys = Keys(("Łódź plan", "C:\\x\\Łódź plan.docx"), ("Inne", "C:\\x\\inne.txt"));
        Assert.Equal([0], ZoneSearch.Filter(keys, "lodz"));
        Assert.Equal([0], ZoneSearch.Filter(keys, "ŁÓDŹ"));
        Assert.Equal([0], ZoneSearch.Filter(keys, "łódź"));
    }

    [Fact]
    public void SeveralWords_MustAllMatch_InAnyOrder()
    {
        var keys = Keys(("Raport roczny", "C:\\x\\raport roczny.pdf"), ("Raport miesięczny", "C:\\x\\raport.xlsx"), ("Faktura", "C:\\x\\faktura.pdf"));
        Assert.Equal([0], ZoneSearch.Filter(keys, "pdf raport"));
        Assert.Equal([0, 1], ZoneSearch.Filter(keys, "raport"));
        Assert.Empty(ZoneSearch.Filter(keys, "raport faktura"));
    }

    [Fact]
    public void Matches_FileNameWithExtension_NotJustTheDisplayName()
    {
        // A shortcut is shown without ".lnk", a file may be shown without its extension: both must still be findable.
        var keys = Keys(("Visual Studio Code", "C:\\Users\\x\\Desktop\\Visual Studio Code.lnk"), ("Notes", "C:\\docs\\notes.txt"));
        Assert.Equal([0], ZoneSearch.Filter(keys, ".lnk"));
        Assert.Equal([1], ZoneSearch.Filter(keys, "notes.txt"));
    }

    [Fact]
    public void Matches_FoldersByTheirName_NotByTheParentPath()
    {
        var keys = Keys(("Projects", "C:\\Work\\Projects"), ("Other", "C:\\Work\\Projects\\Other"));
        Assert.Equal([0], ZoneSearch.Filter(keys, "projects")); // "Other" lives inside Projects but is not named so
    }

    [Fact]
    public void NoMatches_ReturnsEmpty()
        => Assert.Empty(ZoneSearch.Filter(Keys(("a", "C:\\a.txt")), "zzz"));

    [Fact]
    public void Filter_DoesNotChangeItsInput()
    {
        var keys = Keys(("Beta", "C:\\b"), ("Alpha", "C:\\a"));
        var copy = keys.ToList();
        _ = ZoneSearch.Filter(keys, "alpha");
        Assert.Equal(copy, keys);
    }

    [Fact]
    public void BuildKey_ToleratesMissingParts()
    {
        Assert.Equal(string.Empty, ZoneSearch.BuildKey(null, null).Trim());
        Assert.Contains("only", ZoneSearch.BuildKey("Only", null));
        Assert.Contains("file", ZoneSearch.BuildKey(null, "C:\\x\\File.txt"));
    }

    [Fact]
    public void TwoThousandItems_FilterInWellUnderAFrame()
    {
        var keys = Enumerable.Range(0, 2000)
            .Select(i => ZoneSearch.BuildKey($"Stress file {i:D4}", $"C:\\stress\\Stress file {i:D4}{(i % 7 == 0 ? " report.pdf" : ".txt")}"))
            .ToList();

        _ = ZoneSearch.Filter(keys, "warm"); // JIT
        var sw = Stopwatch.StartNew();
        var hits = ZoneSearch.Filter(keys, "stress report");
        sw.Stop();

        Assert.Equal(286, hits.Count);
        Assert.True(sw.ElapsedMilliseconds < 50, $"took {sw.ElapsedMilliseconds} ms");
    }
}

/// <summary>
/// The search is a VIEW over the zone: it must never write configuration, reorder items, touch icons or files, and (per the benchmark)
/// must hide tiles instead of using a collection-view filter, which rebuilt all containers on every key.
/// </summary>
public class SearchIsViewOnlyTests
{
    private static string AppFile(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NextMind.Desktop.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repo root not found."), "src", "NextMind.Desktop.App", name));
    }

    private static string Body(string source, string signature)
    {
        var match = Regex.Match(source, Regex.Escape(signature) + @"[\s\S]*?\n    \}");
        Assert.True(match.Success, "method not found: " + signature);
        return match.Value;
    }

    [Theory]
    [InlineData("private void ApplyFilter()")]
    [InlineData("private void SearchBox_TextChanged(")]
    public void FilteringCode_NeverWritesConfig_ReordersOrReloadsIcons(string signature)
    {
        var body = Body(AppFile("ZoneWindow.xaml.cs"), signature);
        foreach (var forbidden in new[] { "ZoneOrdering", "ZoneItems.", "ZoneChanged", "_config", "_icons", "SyncItems", "File.", "Directory." })
        {
            Assert.DoesNotContain(forbidden, body);
        }
    }

    [Fact]
    public void Filtering_HidesTiles_ByContainerStyle_NotByCollectionViewFilter()
    {
        var code = AppFile("ZoneWindow.xaml.cs");
        Assert.DoesNotContain("CollectionViewSource", code);
        Assert.DoesNotContain("ICollectionView", code);
        Assert.Matches(@"DataTrigger Binding=""\{Binding IsFilteredOut\}"" Value=""True""", AppFile("ZoneWindow.xaml"));
    }

    [Fact]
    public void ReorderingByDrag_IsOff_WhileAFilterIsActive()
    {
        var code = AppFile("ZoneWindow.xaml.cs");
        Assert.Matches(@"Items_PreviewMouseMove[\s\S]{0,700}IsFiltering", code);
    }
}
