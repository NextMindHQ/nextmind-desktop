using System.Runtime.InteropServices;
using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>
/// Keeps zone windows at "desktop level": above the desktop (wallpaper + icons), below every ordinary window,
/// without reparenting into Explorer's windows and without any code running inside Explorer.
///
/// *** UNDOCUMENTED WINDOWS BEHAVIOUR ***
/// Microsoft does not document the shell's desktop window structure. We rely on two observations:
///   1. The desktop icons are drawn by a "SHELLDLL_DefView" child whose parent is either "Progman" or (when an
///      animated/slideshow wallpaper is active) a top-level "WorkerW". We locate whichever top-level window owns it.
///   2. A top-level window inserted directly above that host in the z-order is drawn over the icons but stays
///      below every window the user activates. Windows only ever raises OTHER windows above it.
/// Both are re-validated on every use, so an Explorer restart or a wallpaper-mode switch is handled without
/// polling. If the host cannot be found the layer reports failure and the caller keeps a visible fallback
/// (see <see cref="Pin"/>): we never bottom-pin blindly, because a window under the desktop host is invisible.
/// </summary>
public sealed class DesktopLayer
{
    /// <summary>Re-resolve at most this often when the host cannot be found (message storms during drags).</summary>
    private const long ResolveThrottleMs = 1000;

    private IntPtr _host;
    private long _lastFailedResolveTicks;

    /// <summary>Drops the cached host (call after <c>TaskbarCreated</c>, i.e. an Explorer restart).</summary>
    public void Invalidate()
    {
        _host = IntPtr.Zero;
        _lastFailedResolveTicks = 0;
    }

    /// <summary>The top-level window hosting the desktop icon view, or <see cref="IntPtr.Zero"/> if none was found.</summary>
    public IntPtr ResolveHost()
    {
        if (HostsDefView(_host))
        {
            return _host;
        }

        var now = Environment.TickCount64;
        if (_lastFailedResolveTicks != 0 && now - _lastFailedResolveTicks < ResolveThrottleMs)
        {
            return IntPtr.Zero;
        }

        _host = FindHost();
        _lastFailedResolveTicks = _host == IntPtr.Zero ? now : 0;
        return _host;
    }

    /// <summary>Human-readable description for logs: which window we anchor on and what is directly above it.</summary>
    public string Describe()
    {
        var host = ResolveHost();
        if (host == IntPtr.Zero)
        {
            return "desktop host: NOT FOUND";
        }

        var above = NativeMethods.GetWindow(host, NativeMethods.GW_HWNDPREV);
        return $"desktop host: {NativeMethods.GetClassName(host)} 0x{host.ToInt64():X}; directly above it: {NativeMethods.GetClassName(above)} 0x{above.ToInt64():X}";
    }

    /// <summary>
    /// Removes the window from Alt+Tab and the taskbar and makes it non-activating (clicks never steal focus).
    /// WS_EX_TOOLWINDOW is used rather than ShowInTaskbar=false: WPF implements the latter with a hidden owner
    /// window, and an owned window can be dragged up the z-order together with its owner.
    /// Call before the window is first shown.
    /// </summary>
    public static void ApplyDesktopWindowStyles(IntPtr hwnd)
    {
        var ex = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        ex |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        ex &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(ex));

        NativeMethods.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
    }

    /// <summary>
    /// Places the window directly above the desktop host. Returns false when the host cannot be found; in that case
    /// the window is left where Windows put it (a visible, ordinary-z-order window) — the safe fallback.
    /// </summary>
    public bool Pin(IntPtr hwnd)
    {
        var insertAfter = TargetInsertAfter(hwnd, out var alreadyInPlace);
        if (insertAfter is null)
        {
            return false;
        }

        if (alreadyInPlace)
        {
            return true;
        }

        NativeMethods.SetWindowPos(
            hwnd,
            insertAfter.Value,
            0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
        return true;
    }

    /// <summary>
    /// For the window's WM_WINDOWPOSCHANGING: rewrites a pending z-order change so activation or a stray
    /// SetWindowPos(HWND_TOP) can never lift the window above ordinary windows. Plain moves/resizes
    /// (SWP_NOZORDER) are left untouched. Let the message continue to normal processing afterwards.
    /// </summary>
    public bool EnforceOnWindowPosChanging(IntPtr hwnd, IntPtr lParam)
    {
        if (lParam == IntPtr.Zero)
        {
            return false;
        }

        var pos = Marshal.PtrToStructure<NativeMethods.WINDOWPOS>(lParam);
        if ((pos.flags & NativeMethods.SWP_NOZORDER) != 0)
        {
            return false;
        }

        var insertAfter = TargetInsertAfter(hwnd, out var alreadyInPlace);
        if (insertAfter is null)
        {
            return false;
        }

        if (alreadyInPlace)
        {
            pos.flags |= NativeMethods.SWP_NOZORDER;
        }
        else
        {
            pos.hwndInsertAfter = insertAfter.Value;
        }

        Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
        return true;
    }

    private IntPtr? TargetInsertAfter(IntPtr hwnd, out bool alreadyInPlace)
    {
        alreadyInPlace = false;

        var host = ResolveHost();
        if (host == IntPtr.Zero || host == hwnd)
        {
            return null;
        }

        // SetWindowPos places a window BEHIND hWndInsertAfter, so to sit directly above the host we insert
        // after the window that is currently directly above it.
        var above = NativeMethods.GetWindow(host, NativeMethods.GW_HWNDPREV);
        if (above == hwnd)
        {
            alreadyInPlace = true;
            return above;
        }

        return above == IntPtr.Zero ? NativeMethods.HWND_TOP : above;
    }

    private static bool HostsDefView(IntPtr hwnd)
        => hwnd != IntPtr.Zero
           && NativeMethods.IsWindow(hwnd)
           && NativeMethods.FindWindowExW(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;

    private static IntPtr FindHost()
    {
        var progman = NativeMethods.GetShellWindow();
        if (progman == IntPtr.Zero)
        {
            progman = NativeMethods.FindWindowW("Progman", null);
        }

        if (HostsDefView(progman))
        {
            return progman;
        }

        var worker = IntPtr.Zero;
        while ((worker = NativeMethods.FindWindowExW(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
        {
            if (HostsDefView(worker))
            {
                return worker;
            }
        }

        return IntPtr.Zero;
    }
}
