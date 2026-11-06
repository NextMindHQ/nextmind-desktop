using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Shell;
using NextMind.Desktop.App;

namespace NextMind.Desktop.Tests;

public sealed class FutureWindowChromeTests
{
    [Fact]
    public void ApplyUsesBorderlessResizableDpiIndependentWindowChrome()
    {
        RunOnStaThread(() =>
        {
            var window = new Window();

            FutureWindowChrome.Apply(window);

            Assert.Equal(WindowStyle.None, window.WindowStyle);
            Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
            var chrome = Assert.IsType<WindowChrome>(WindowChrome.GetWindowChrome(window));
            Assert.Equal(44, chrome.CaptionHeight);
            Assert.Equal(new Thickness(7), chrome.ResizeBorderThickness);
            Assert.Equal(new Thickness(0), chrome.GlassFrameThickness);
            Assert.Equal(new CornerRadius(0), chrome.CornerRadius);
            Assert.False(chrome.UseAeroCaptionButtons);
        });
    }

    [Fact]
    public void ToggleMaximizeAlternatesBetweenMaximizedAndRestoredStates()
    {
        RunOnStaThread(() =>
        {
            var window = new Window { WindowState = WindowState.Normal };

            FutureWindowChrome.ToggleMaximize(window);
            Assert.Equal(WindowState.Maximized, window.WindowState);

            FutureWindowChrome.ToggleMaximize(window);
            Assert.Equal(WindowState.Normal, window.WindowState);
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
