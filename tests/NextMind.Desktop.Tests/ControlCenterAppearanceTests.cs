using NextMind.Desktop.App;
using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public sealed class ControlCenterAppearanceTests
{
    [Fact]
    public void FromUi_MapsPercentControlsToPersistedAppearance()
    {
        var appearance = AppearanceEditor.FromUi(72, 81, AppearanceAccent.Cyan, animations: false, resourceSaver: true);

        Assert.Equal(0.72, appearance.GlowIntensity);
        Assert.Equal(0.81, appearance.PanelOpacity);
        Assert.Equal(AppearanceAccent.Cyan, appearance.Accent);
        Assert.False(appearance.AnimationsEnabled);
        Assert.True(appearance.ResourceSaverMode);
    }

    [Fact]
    public void Reset_ReturnsDocumentedV3Defaults()
    {
        var appearance = AppearanceEditor.Reset();

        Assert.Equal(0.55, appearance.GlowIntensity);
        Assert.Equal(0.90, appearance.PanelOpacity);
        Assert.Equal(AppearanceAccent.ElectricBlue, appearance.Accent);
        Assert.True(appearance.AnimationsEnabled);
        Assert.False(appearance.ResourceSaverMode);
    }
}
