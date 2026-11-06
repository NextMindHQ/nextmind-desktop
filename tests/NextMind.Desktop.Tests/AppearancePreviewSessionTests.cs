using NextMind.Desktop.App;
using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public sealed class AppearancePreviewSessionTests
{
    [Fact]
    public void Preview_AppliesTemporaryValueWithoutPersisting()
    {
        var previews = new List<AppearancePreferencesConfig>();
        var persisted = new List<AppearancePreferencesConfig>();
        var session = new AppearancePreviewSession(
            Saved(AppearanceAccent.ElectricBlue, 0.55, 0.90),
            previews.Add,
            persisted.Add);

        session.Preview(Saved(AppearanceAccent.Violet, 0.72, 0.81));

        var preview = Assert.Single(previews);
        Assert.Equal(AppearanceAccent.Violet, preview.Accent);
        Assert.Equal(0.72, preview.GlowIntensity);
        Assert.Empty(persisted);
    }

    [Fact]
    public void Commit_PersistsCurrentValueAndMakesItTheRollbackBaseline()
    {
        var previews = new List<AppearancePreferencesConfig>();
        var persisted = new List<AppearancePreferencesConfig>();
        var session = new AppearancePreviewSession(
            Saved(AppearanceAccent.ElectricBlue, 0.55, 0.90),
            previews.Add,
            persisted.Add);

        session.Preview(Saved(AppearanceAccent.Cyan, 0.65, 0.85));
        session.Commit(Saved(AppearanceAccent.Cyan, 0.65, 0.85));
        session.Preview(Saved(AppearanceAccent.Amber, 0.20, 0.60));
        session.Rollback();

        var saved = Assert.Single(persisted);
        Assert.Equal(AppearanceAccent.Cyan, saved.Accent);
        Assert.Equal(AppearanceAccent.Cyan, previews[^1].Accent);
        Assert.Equal(0.85, previews[^1].PanelOpacity);
    }

    [Fact]
    public void Reset_IsOnlyPreviewUntilCommit()
    {
        var previews = new List<AppearancePreferencesConfig>();
        var persisted = new List<AppearancePreferencesConfig>();
        var session = new AppearancePreviewSession(
            Saved(AppearanceAccent.Amber, 0.2, 0.7),
            previews.Add,
            persisted.Add);

        session.Preview(AppearanceEditor.Reset());

        Assert.Equal(AppearanceAccent.ElectricBlue, Assert.Single(previews).Accent);
        Assert.Empty(persisted);
    }

    [Fact]
    public void Rollback_DoesNothingWhenNothingChangedAfterCommit()
    {
        var previews = new List<AppearancePreferencesConfig>();
        var persisted = new List<AppearancePreferencesConfig>();
        var session = new AppearancePreviewSession(
            Saved(AppearanceAccent.ElectricBlue, 0.55, 0.90),
            previews.Add,
            persisted.Add);

        session.Commit(Saved(AppearanceAccent.Violet, 0.70, 0.80));
        previews.Clear();
        session.Rollback();

        Assert.Empty(previews);
        Assert.Single(persisted);
    }

    [Fact]
    public void Session_CopiesValuesSoUiMutationCannotChangeSavedBaseline()
    {
        var previews = new List<AppearancePreferencesConfig>();
        var original = Saved(AppearanceAccent.Cyan, 0.60, 0.75);
        var session = new AppearancePreviewSession(original, previews.Add, _ => { });
        original.Accent = AppearanceAccent.Amber;

        session.Preview(Saved(AppearanceAccent.Violet, 0.8, 0.8));
        session.Rollback();

        Assert.Equal(AppearanceAccent.Cyan, previews[^1].Accent);
    }

    private static AppearancePreferencesConfig Saved(AppearanceAccent accent, double glow, double opacity) => new()
    {
        Accent = accent,
        GlowIntensity = glow,
        PanelOpacity = opacity,
        AnimationsEnabled = true,
        ResourceSaverMode = false,
    };
}
