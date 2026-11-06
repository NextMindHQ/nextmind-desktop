using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.App;

public static class AppearanceEditor
{
    public static AppearancePreferencesConfig FromUi(double glowPercent, double opacityPercent, AppearanceAccent accent, bool animations, bool resourceSaver)
        => new()
        {
            GlowIntensity = Math.Clamp(glowPercent / 100, 0, 1),
            PanelOpacity = Math.Clamp(opacityPercent / 100, 0.55, 1),
            Accent = accent,
            AnimationsEnabled = animations,
            ResourceSaverMode = resourceSaver,
        };

    public static AppearancePreferencesConfig Reset() => new();
}
