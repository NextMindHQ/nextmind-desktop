using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Core.Widgets;

namespace NextMind.Desktop.Tests;

public sealed class WidgetCatalogTests
{
    [Fact]
    public void Catalog_ContainsOnlyFunctionalLocalWidgets_WithSaneDefaultSizes()
    {
        var definitions = WidgetCatalog.Definitions;

        Assert.Equal(new[] { WidgetKind.Clock, WidgetKind.QuickNote }, definitions.Select(definition => definition.Kind));
        Assert.All(definitions, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.InRange(definition.DefaultWidth, ZoneLimits.MinWidth, 1000);
            Assert.InRange(definition.DefaultHeight, ZoneLimits.MinHeight, 1000);
        });
    }

    [Fact]
    public void Create_AssignsUniqueIds_AndCascadesInsideWorkArea()
    {
        var existing = new List<WidgetConfig>();
        var area = new RectPx(-800, 0, 800, 600);

        var first = WidgetCatalog.Create(WidgetKind.Clock, existing, area, 144);
        existing.Add(first);
        var second = WidgetCatalog.Create(WidgetKind.QuickNote, existing, area, 144);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(144, first.Dpi);
        Assert.True(area.IntersectionArea(new RectPx(first.X, first.Y, first.Width, first.Height)) > 0);
        Assert.Equal((first.X + 36, first.Y + 36), (second.X, second.Y));
    }

    [Fact]
    public void QuickNoteText_RoundTripsAndIsBoundedByValidation()
    {
        var config = new AppConfig
        {
            Widgets =
            [
                new WidgetConfig { Id = "note", Kind = WidgetKind.QuickNote, Hidden = true, NoteText = "Zażółć 📝\n" + new string('x', 20_000) },
            ],
        };

        var back = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));

        var note = Assert.Single(back.Widgets);
        Assert.True(note.Hidden);
        Assert.StartsWith("Zażółć 📝\n", note.NoteText);
        Assert.Equal(WidgetLimits.MaxNoteLength, note.NoteText.Length);
    }

    [Fact]
    public void Profiles_SaveAndRestoreIndependentWidgetCopies()
    {
        var config = new AppConfig
        {
            Widgets = [new WidgetConfig { Id = "home-clock", Kind = WidgetKind.Clock, X = 10 }],
        };
        WorkspaceProfiles.EnsureInitialized(config);
        var homeId = config.ActiveProfileId;

        var work = WorkspaceProfiles.Create(config, "Work");
        config.Widgets.Add(new WidgetConfig { Id = "work-note", Kind = WidgetKind.QuickNote, NoteText = "Work" });

        Assert.True(WorkspaceProfiles.Switch(config, homeId));
        Assert.Equal("home-clock", Assert.Single(config.Widgets).Id);
        config.Widgets[0].X = 999;

        Assert.True(WorkspaceProfiles.Switch(config, work.Id));
        Assert.Equal("work-note", Assert.Single(config.Widgets).Id);
        Assert.Equal(999, Assert.Single(config.Profiles.Single(profile => profile.Id == homeId).Widgets).X);
    }
}
