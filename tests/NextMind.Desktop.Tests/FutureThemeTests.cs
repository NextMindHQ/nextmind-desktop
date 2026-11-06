using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using NextMind.Desktop.App;
using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public sealed class FutureThemeTests
{
    private static readonly string[] RequiredComponentKeys =
    [
        "HolographicPanelStyle",
        "FuturisticButtonStyle",
        "FuturisticPrimaryButtonStyle",
        "FuturisticDangerButtonStyle",
        "FuturisticTextBoxStyle",
        "FuturisticToggleStyle",
        "FuturisticSliderStyle",
        "FuturisticMenuStyle",
        "FuturisticMenuItemStyle",
        "FuturisticTooltipStyle",
        "FuturisticWindowHeaderStyle",
        "FuturisticSearchResultStyle",
        "FuturisticWidgetContainerStyle",
        "FuturisticZoneItemStyle",
    ];

    [Fact]
    public void ResourceDictionary_ExposesEveryV3ComponentStyle()
    {
        RunOnStaThread(() =>
        {
            var dictionary = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
                new Uri("/NextMindDesktop;component/Themes/FutureTheme.xaml", UriKind.Relative)));

            foreach (var key in RequiredComponentKeys)
            {
                Assert.True(dictionary.Contains(key), $"Missing shared V3 style: {key}");
            }
        });
    }

    [Fact]
    public void Apply_MapsAppearanceToBoundedDynamicResources()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary();
            FutureTheme.Apply(resources, new AppearancePreferencesConfig
            {
                Accent = AppearanceAccent.Violet,
                GlowIntensity = 0.5,
                PanelOpacity = 0.8,
                AnimationsEnabled = true,
            });

            Assert.Equal(Color.FromRgb(0xA7, 0x78, 0xFF), resources["FutureAccentColor"]);
            Assert.Equal(0.8, resources["FuturePanelOpacity"]);
            Assert.Equal(0.35, resources["FutureGlowOpacity"]);
            Assert.Equal(TimeSpan.FromMilliseconds(160), ((Duration)resources["FutureMotionDuration"]).TimeSpan);
        });
    }

    [Fact]
    public void Apply_ResourceSaverDisablesDecorativeGlowAndMotion()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary();
            FutureTheme.Apply(resources, new AppearancePreferencesConfig
            {
                GlowIntensity = 1,
                AnimationsEnabled = true,
                ResourceSaverMode = true,
            });

            Assert.Equal(0d, Assert.IsType<double>(resources["FutureGlowOpacity"]));
            Assert.Equal(TimeSpan.Zero, ((Duration)resources["FutureMotionDuration"]).TimeSpan);
            Assert.False((bool)resources["FutureEffectsEnabled"]);
        });
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
