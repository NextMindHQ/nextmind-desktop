using NextMind.Desktop.App;
using NextMind.Desktop.Core.Geometry;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;

namespace NextMind.Desktop.Tests;

public sealed class ClockWidgetLayoutTests
{
    [Fact]
    public void CompactContentKeepsEveryTextLevelReadableWithinItsHeightBudget()
    {
        var metrics = ClockWidgetLayout.Calculate(136, 60);

        Assert.Equal(27, metrics.TimeFontSize, 3);
        Assert.Equal(9, metrics.LabelFontSize, 3);
        Assert.Equal(11, metrics.DateFontSize, 3);
        Assert.Equal(3, metrics.DateMargin, 3);
        Assert.InRange(metrics.EstimatedTextHeight, 0, 60);
    }

    [Fact]
    public void TypographyGrowsMonotonicallyFromCompactToDefaultAndLargeContent()
    {
        var compact = ClockWidgetLayout.Calculate(136, 60);
        var normal = ClockWidgetLayout.Calculate(236, 110);
        var large = ClockWidgetLayout.Calculate(496, 270);
        var extraLarge = ClockWidgetLayout.Calculate(1008, 540);

        Assert.Equal(49.5, normal.TimeFontSize, 3);
        Assert.Equal(118.095, large.TimeFontSize, 3);
        Assert.Equal(240, extraLarge.TimeFontSize, 3);
        Assert.True(normal.TimeFontSize > compact.TimeFontSize);
        Assert.True(large.TimeFontSize > normal.TimeFontSize);
        Assert.True(extraLarge.TimeFontSize > large.TimeFontSize);
        Assert.True(normal.LabelFontSize > compact.LabelFontSize);
        Assert.True(large.LabelFontSize > normal.LabelFontSize);
        Assert.True(extraLarge.LabelFontSize > large.LabelFontSize);
        Assert.True(normal.DateFontSize > compact.DateFontSize);
        Assert.True(large.DateFontSize > normal.DateFontSize);
        Assert.True(extraLarge.DateFontSize > large.DateFontSize);
    }

    [Theory]
    [InlineData(136, 60)]
    [InlineData(236, 110)]
    [InlineData(496, 270)]
    [InlineData(1008, 540)]
    public void SupportedSizesPreserveTheVerticalContentBudget(double width, double height)
    {
        var metrics = ClockWidgetLayout.Calculate(width, height);

        Assert.InRange(metrics.EstimatedTextHeight, 0, height);
    }

    [Fact]
    public void InvalidOrTinyMeasurementsFallBackToCompactFiniteMetrics()
    {
        var tiny = ClockWidgetLayout.Calculate(0, -1);
        var invalid = ClockWidgetLayout.Calculate(double.NaN, double.PositiveInfinity);

        Assert.Equal(27, tiny.TimeFontSize, 3);
        Assert.Equal(tiny, invalid);
        Assert.True(double.IsFinite(tiny.EstimatedTextHeight));
    }

    [Fact]
    public void EquivalentPhysicalSizesAtDifferentDpiProduceIdenticalTypography()
    {
        var at96 = ClockWidgetLayout.Calculate(
            ZoneGeometry.PxToDip(496, 96),
            ZoneGeometry.PxToDip(270, 96));
        var at144 = ClockWidgetLayout.Calculate(
            ZoneGeometry.PxToDip(744, 144),
            ZoneGeometry.PxToDip(405, 144));
        var at192 = ClockWidgetLayout.Calculate(
            ZoneGeometry.PxToDip(992, 192),
            ZoneGeometry.PxToDip(540, 192));

        Assert.Equal(at96, at144);
        Assert.Equal(at96, at192);
    }

    [Fact]
    public void RealClockContentUpdatesTypographyImmediatelyWhenArrangedAtANewSize()
    {
        RunOnStaThread(() =>
        {
            var content = new ClockWidgetContent();
            var label = Assert.IsType<TextBlock>(content.FindName("ClockLabel"));
            var time = Assert.IsType<TextBlock>(content.FindName("ClockTime"));
            var date = Assert.IsType<TextBlock>(content.FindName("ClockDate"));

            Arrange(content, 136, 60);
            var compact = (label.FontSize, time.FontSize, date.FontSize);

            Arrange(content, 496, 270);

            Assert.Equal((9, 27, 11), compact);
            Assert.Equal(28.343, label.FontSize, 3);
            Assert.Equal(118.095, time.FontSize, 3);
            Assert.Equal(40.152, date.FontSize, 3);
        });
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
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
