namespace NextMind.Desktop.App;

public readonly record struct ZoneEmptyStatePresentation(
    string Message,
    bool IsVisible,
    bool ShowDropAffordance);

public static class ZoneEmptyState
{
    public static ZoneEmptyStatePresentation Resolve(
        bool filterActive,
        string query,
        int visibleMatches,
        int itemCount)
    {
        if (filterActive)
        {
            return new(
                $"No matches for “{query.Trim()}”",
                visibleMatches == 0,
                ShowDropAffordance: false);
        }

        return new(
            "DROP MODULES HERE",
            itemCount == 0,
            ShowDropAffordance: true);
    }
}
