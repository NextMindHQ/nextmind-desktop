using NextMind.Desktop.App;

namespace NextMind.Desktop.Tests;

public sealed class ZoneEmptyStateTests
{
    [Fact]
    public void ActiveFilterWithNoMatchesShowsQueryAndHidesDropAffordance()
    {
        var state = ZoneEmptyState.Resolve(filterActive: true, query: "  report  ", visibleMatches: 0, itemCount: 4);

        Assert.True(state.IsVisible);
        Assert.False(state.ShowDropAffordance);
        Assert.Equal("No matches for “report”", state.Message);
    }

    [Fact]
    public void EmptyUnfilteredZoneShowsV3DropAffordance()
    {
        var state = ZoneEmptyState.Resolve(filterActive: false, query: string.Empty, visibleMatches: 0, itemCount: 0);

        Assert.True(state.IsVisible);
        Assert.True(state.ShowDropAffordance);
        Assert.Equal("DROP MODULES HERE", state.Message);
    }

    [Fact]
    public void PopulatedUnfilteredZoneHidesEmptyState()
    {
        var state = ZoneEmptyState.Resolve(filterActive: false, query: string.Empty, visibleMatches: 1, itemCount: 1);

        Assert.False(state.IsVisible);
    }
}
