using NextMind.Desktop.Core.Geometry;
using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>Monitor work areas (the screen minus taskbar) in physical pixels of the virtual screen.</summary>
public static class Monitors
{
    public static IReadOnlyList<RectPx> GetWorkAreas()
    {
        var list = new List<RectPx>();

        bool Callback(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
        {
            var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
            if (NativeMethods.GetMonitorInfoW(hMonitor, ref info))
            {
                list.Add(ToRect(info.rcWork));
            }

            return true;
        }

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, Callback, IntPtr.Zero);
        return list;
    }

    public static RectPx GetPrimaryWorkArea()
    {
        var monitor = NativeMethods.MonitorFromPoint(default, NativeMethods.MONITOR_DEFAULTTOPRIMARY);
        var info = new NativeMethods.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        return NativeMethods.GetMonitorInfoW(monitor, ref info) ? ToRect(info.rcWork) : new RectPx(0, 0, 1280, 720);
    }

    public static int GetDpi(IntPtr hwnd)
    {
        var dpi = hwnd == IntPtr.Zero ? 0u : NativeMethods.GetDpiForWindow(hwnd);
        return dpi == 0 ? 96 : (int)dpi;
    }

    public static int GetSystemDpi()
    {
        var dpi = NativeMethods.GetDpiForSystem();
        return dpi == 0 ? 96 : (int)dpi;
    }

    public static RectPx GetWindowRect(IntPtr hwnd)
        => NativeMethods.GetWindowRect(hwnd, out var r) ? ToRect(r) : default;

    /// <summary>Moves/resizes in physical pixels without touching z-order or activation.</summary>
    public static void SetWindowBounds(IntPtr hwnd, RectPx rect)
        => NativeMethods.SetWindowPos(
            hwnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height,
            NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);

    private static RectPx ToRect(NativeMethods.RECT r) => new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
}
