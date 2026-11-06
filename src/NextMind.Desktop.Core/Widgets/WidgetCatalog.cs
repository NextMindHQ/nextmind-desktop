using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Core.Widgets;

public sealed record WidgetDefinition(WidgetKind Kind, string Title, string Description, int DefaultWidth, int DefaultHeight);

public static class WidgetCatalog
{
    public static IReadOnlyList<WidgetDefinition> Definitions { get; } =
    [
        new(WidgetKind.Clock, "Clock & Date", "Local time and date", 260, 150),
        new(WidgetKind.QuickNote, "Quick Note", "A note stored only in NextMind Desktop", 300, 220),
    ];

    public static WidgetConfig Create(WidgetKind kind, IReadOnlyList<WidgetConfig> existing, RectPx workArea, int dpi)
    {
        var definition = Definitions.Single(candidate => candidate.Kind == kind);
        var offset = existing.Count * 36;
        var proposed = new RectPx(
            workArea.X + 24 + offset,
            workArea.Y + 24 + offset,
            ZoneGeometry.DipToPx(definition.DefaultWidth, dpi),
            ZoneGeometry.DipToPx(definition.DefaultHeight, dpi));
        var bounds = ZoneGeometry.ClampToWorkAreas(proposed, [workArea]);
        return new WidgetConfig
        {
            Kind = kind,
            X = bounds.X,
            Y = bounds.Y,
            Width = bounds.Width,
            Height = bounds.Height,
            Dpi = dpi,
        };
    }
}
