using System.Windows;
using System.Windows.Shell;

namespace NextMind.Desktop.App;

public static class FutureWindowChrome
{
    public const double CaptionHeightDip = 44;
    public const double ResizeBorderDip = 7;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = CaptionHeightDip,
            ResizeBorderThickness = new Thickness(ResizeBorderDip),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });
    }

    public static void ToggleMaximize(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
