using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.App;

public static class FutureTheme
{
    private static readonly Color ElectricBlue = Color.FromRgb(0x38, 0xBD, 0xF8);
    private static readonly Color Cyan = Color.FromRgb(0x22, 0xD3, 0xC5);
    private static readonly Color Violet = Color.FromRgb(0xA7, 0x78, 0xFF);
    private static readonly Color Amber = Color.FromRgb(0xF4, 0xB8, 0x60);

    public static void Apply(AppearancePreferencesConfig appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        if (Application.Current is null)
        {
            return;
        }

        Apply(Application.Current.Resources, appearance);
    }

    public static void Apply(ResourceDictionary resources, AppearancePreferencesConfig appearance)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(appearance);

        var accent = appearance.Accent switch
        {
            AppearanceAccent.Cyan => Cyan,
            AppearanceAccent.Violet => Violet,
            AppearanceAccent.Amber => Amber,
            _ => ElectricBlue,
        };
        var panelOpacity = Math.Clamp(appearance.PanelOpacity, 0.55, 1);
        var effectsEnabled = !appearance.ResourceSaverMode;
        var glowOpacity = effectsEnabled ? Math.Clamp(appearance.GlowIntensity, 0, 1) * 0.7 : 0;
        var motion = appearance.AnimationsEnabled && effectsEnabled
            ? new Duration(TimeSpan.FromMilliseconds(160))
            : new Duration(TimeSpan.Zero);

        resources["FutureAccentColor"] = accent;
        resources["FutureAccentBrush"] = Brush(accent);
        resources["FutureAccentStrongBrush"] = Brush(Color.FromArgb(0xDD, accent.R, accent.G, accent.B));
        resources["FutureAccentSoftBrush"] = Brush(Color.FromArgb(0x33, accent.R, accent.G, accent.B));
        resources["FuturePanelOpacity"] = panelOpacity;
        resources["FuturePanelBrush"] = Brush(Color.FromArgb(Alpha(panelOpacity), 0x10, 0x19, 0x25));
        resources["FuturePanelAltBrush"] = Brush(Color.FromArgb(Alpha(Math.Max(0.55, panelOpacity - 0.07)), 0x17, 0x22, 0x30));
        resources["FutureGlowOpacity"] = glowOpacity;
        resources["FutureMotionDuration"] = motion;
        resources["FutureEffectsEnabled"] = effectsEnabled;
        resources["FuturePanelEffect"] = new DropShadowEffect
        {
            Color = accent,
            BlurRadius = effectsEnabled ? 18 : 0,
            ShadowDepth = 0,
            Opacity = glowOpacity,
            RenderingBias = RenderingBias.Performance,
        };
    }

    public static void BeginEntrance(UIElement element, AppearancePreferencesConfig appearance)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(appearance);
        if (!appearance.AnimationsEnabled || appearance.ResourceSaverMode)
        {
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private static byte Alpha(double opacity) => (byte)Math.Round(255 * opacity, MidpointRounding.AwayFromZero);

    private static SolidColorBrush Brush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
