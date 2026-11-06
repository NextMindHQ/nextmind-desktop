using NextMind.Desktop.Core.Search;

namespace NextMind.Desktop.Tests;

public sealed class SearchIndexTests
{
    private static readonly SearchEntry[] Entries =
    [
        new("cmd:create-zone", "Create Zone", "Desktop command", SearchEntryKind.Command, ["new panel"]),
        new("zone:nextmind", "🦈 NextMind", "Zone", SearchEntryKind.Zone, ["work ai"]),
        new("app:notepad", "Notepad", "Application", SearchEntryKind.Application, ["editor notes"]),
        new("widget:note", "Quick Note", "Widget", SearchEntryKind.Widget, ["notes memo"]),
    ];

    [Fact]
    public void EmptyOrWhitespaceQuery_ReturnsNoResults()
    {
        var index = new SearchIndex(Entries);

        Assert.Empty(index.Query(""));
        Assert.Empty(index.Query("   "));
    }

    [Fact]
    public void Query_NormalizesCaseDiacriticsAndMultipleTokens()
    {
        var index = new SearchIndex(
        [
            new SearchEntry("zone:polish", "Zażółć Projekty", "Strefa", SearchEntryKind.Zone, ["praca"]),
        ]);

        var result = index.Query("ZAZOLC praca");

        Assert.Equal("zone:polish", Assert.Single(result).Id);
    }

    [Fact]
    public void Query_RanksExactAndTitlePrefixBeforeKeywordMatches()
    {
        var index = new SearchIndex(Entries);

        var result = index.Query("note");

        Assert.Equal(new[] { "app:notepad", "widget:note" }, result.Select(entry => entry.Id));
    }

    [Fact]
    public void Query_AppliesLimitAfterDeterministicRanking()
    {
        var index = new SearchIndex(Entries);

        var result = index.Query("n", limit: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(result, index.Query("n", limit: 2));
    }

    [Fact]
    public void Query_WithNoMatch_ReturnsEmptyList()
    {
        var index = new SearchIndex(Entries);

        Assert.Empty(index.Query("definitely missing"));
    }
}
