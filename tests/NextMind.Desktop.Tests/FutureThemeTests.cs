using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
        "FuturisticAccentSelectorStyle",
        "FuturisticAccentItemStyle",
        "FuturisticMenuStyle",
        "FuturisticMenuItemStyle",
        "FuturisticTooltipStyle",
        "FuturisticWindowHeaderStyle",
        "FuturisticSearchResultStyle",
        "FuturisticWidgetContainerStyle",
        "FuturisticZoneItemStyle",
        "FutureChromeButtonStyle",
        "FutureChromeCloseButtonStyle",
        "FuturisticScrollBarStyle",
        "FuturisticScrollViewerStyle",
        "FuturisticResizeThumbStyle",
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
    public void ScrollViewerTemplateKeepsDarkFunctionalScrollPartsAndCorner()
    {
        RunOnStaThread(() =>
        {
            var dictionary = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
                new Uri("/NextMindDesktop;component/Themes/FutureTheme.xaml", UriKind.Relative)));
            var viewer = new ScrollViewer
            {
                Width = 180,
                Height = 120,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                Content = new Border { Width = 600, Height = 500 },
                Style = Assert.IsType<Style>(dictionary["FuturisticScrollViewerStyle"]),
            };
            viewer.Resources.MergedDictionaries.Add(dictionary);
            viewer.Measure(new Size(180, 120));
            viewer.Arrange(new Rect(0, 0, 180, 120));
            viewer.ApplyTemplate();
            viewer.UpdateLayout();

            var vertical = Assert.IsType<ScrollBar>(viewer.Template.FindName("PART_VerticalScrollBar", viewer));
            var horizontal = Assert.IsType<ScrollBar>(viewer.Template.FindName("PART_HorizontalScrollBar", viewer));
            var corner = Assert.IsType<Border>(viewer.Template.FindName("ScrollCorner", viewer));
            Assert.Equal(10, vertical.Width);
            Assert.Equal(10, horizontal.Height);
            Assert.NotEqual(Colors.White, Assert.IsType<SolidColorBrush>(corner.Background).Color);

            AssertScrollBarParts(vertical, Orientation.Vertical, ScrollBar.PageUpCommand, ScrollBar.PageDownCommand, 24, 6);
            AssertScrollBarParts(horizontal, Orientation.Horizontal, ScrollBar.PageLeftCommand, ScrollBar.PageRightCommand, 6, 24);
            Assert.True(viewer.ScrollableHeight > 0);
            Assert.True(viewer.ScrollableWidth > 0);
            viewer.ScrollToVerticalOffset(48);
            viewer.ScrollToHorizontalOffset(36);
            viewer.UpdateLayout();
            Assert.Equal(48, viewer.VerticalOffset);
            Assert.Equal(36, viewer.HorizontalOffset);
        });
    }

    [Fact]
    public void ResizeThumbTemplateDoesNotUseTheSystemThumbChrome()
    {
        RunOnStaThread(() =>
        {
            var dictionary = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
                new Uri("/NextMindDesktop;component/Themes/FutureTheme.xaml", UriKind.Relative)));
            var thumb = new Thumb
            {
                Style = Assert.IsType<Style>(dictionary["FuturisticResizeThumbStyle"]),
            };
            thumb.Resources.MergedDictionaries.Add(dictionary);
            thumb.ApplyTemplate();

            var shell = Assert.IsType<Border>(thumb.Template.FindName("GripShell", thumb));
            Assert.Equal(0, Assert.IsType<SolidColorBrush>(shell.Background).Color.A);
        });
    }

    [Fact]
    public void Apply_MapsAppearanceToWorkspaceOnlyDynamicResources()
    {
        RunOnStaThread(() =>
        {
            var fixedControlCenterEffect = new System.Windows.Media.Effects.DropShadowEffect { Opacity = 0.28 };
            var resources = new ResourceDictionary
            {
                ["FutureAccentColor"] = Color.FromRgb(0x38, 0xBD, 0xF8),
                ["FuturePanelBrush"] = new SolidColorBrush(Color.FromArgb(0xE6, 0x10, 0x19, 0x25)),
                ["FuturePanelAltBrush"] = new SolidColorBrush(Color.FromArgb(0xD8, 0x17, 0x22, 0x30)),
                ["FuturePanelRaisedBrush"] = new SolidColorBrush(Color.FromArgb(0xF0, 0x1A, 0x26, 0x35)),
                ["FutureTextBrush"] = new SolidColorBrush(Color.FromRgb(0xEE, 0xF6, 0xFF)),
                ["FuturePanelEffect"] = fixedControlCenterEffect,
            };
            FutureTheme.Apply(resources, new AppearancePreferencesConfig
            {
                Accent = AppearanceAccent.Violet,
                GlowIntensity = 0.5,
                PanelOpacity = 0.8,
                AnimationsEnabled = true,
            });

            Assert.Equal(Color.FromRgb(0xA7, 0x78, 0xFF), resources["WorkspaceAccentColor"]);
            Assert.Equal(0.8, resources["WorkspaceContentOpacity"]);
            Assert.Equal(0xCC, Assert.IsType<SolidColorBrush>(resources["WorkspaceContentBrush"]).Color.A);
            Assert.Equal(0.35, resources["WorkspaceGlowOpacity"]);
            Assert.Equal(Color.FromRgb(0x38, 0xBD, 0xF8), resources["FutureAccentColor"]);
            Assert.Equal(0xE6, Assert.IsType<SolidColorBrush>(resources["FuturePanelBrush"]).Color.A);
            Assert.Equal(0xD8, Assert.IsType<SolidColorBrush>(resources["FuturePanelAltBrush"]).Color.A);
            Assert.Equal(0xF0, Assert.IsType<SolidColorBrush>(resources["FuturePanelRaisedBrush"]).Color.A);
            Assert.Equal(0xFF, Assert.IsType<SolidColorBrush>(resources["FutureTextBrush"]).Color.A);
            Assert.Same(fixedControlCenterEffect, resources["FuturePanelEffect"]);
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

            Assert.Equal(0d, Assert.IsType<double>(resources["WorkspaceGlowOpacity"]));
            Assert.Equal(TimeSpan.Zero, ((Duration)resources["FutureMotionDuration"]).TimeSpan);
            Assert.False((bool)resources["WorkspaceEffectsEnabled"]);
        });
    }

    [Fact]
    public void LivePreview_UpdatesBoundWorkspaceSurfaceBeforePersistence()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary();
            var surface = new Border { Resources = resources };
            surface.SetResourceReference(Border.BackgroundProperty, "WorkspaceContentBrush");
            var persisted = new List<AppearancePreferencesConfig>();
            var initial = new AppearancePreferencesConfig { PanelOpacity = 0.90, GlowIntensity = 0.55 };
            FutureTheme.Apply(resources, initial);
            var session = new AppearancePreviewSession(initial, appearance => FutureTheme.Apply(resources, appearance), persisted.Add);

            session.Preview(new AppearancePreferencesConfig
            {
                PanelOpacity = 0.55,
                GlowIntensity = 1,
                Accent = AppearanceAccent.Amber,
            });

            Assert.Equal(0x8C, Assert.IsType<SolidColorBrush>(surface.Background).Color.A);
            Assert.Equal(Color.FromRgb(0xF4, 0xB8, 0x60), resources["WorkspaceAccentColor"]);
            Assert.Equal(0.7, resources["WorkspaceGlowOpacity"]);
            Assert.Empty(persisted);

            session.Rollback();
            Assert.Equal(0xE6, Assert.IsType<SolidColorBrush>(surface.Background).Color.A);
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

    private static void AssertScrollBarParts(
        ScrollBar scrollBar,
        Orientation orientation,
        ICommand decreaseCommand,
        ICommand increaseCommand,
        double thumbMinHeight,
        double thumbMinWidth)
    {
        scrollBar.ApplyTemplate();
        var track = Assert.IsType<Track>(scrollBar.Template.FindName("PART_Track", scrollBar));
        Assert.Equal(orientation, track.Orientation);
        Assert.Equal(decreaseCommand, track.DecreaseRepeatButton.Command);
        Assert.Equal(increaseCommand, track.IncreaseRepeatButton.Command);
        Assert.Equal(thumbMinHeight, track.Thumb.MinHeight);
        Assert.Equal(thumbMinWidth, track.Thumb.MinWidth);
        Assert.True(track.Thumb.IsHitTestVisible);
    }
}
