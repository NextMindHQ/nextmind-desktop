using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>Thin wrapper over the foreground-window APIs (handles as plain numbers so Core logic stays Windows-free).</summary>
public static class ForegroundWindow
{
    public static long Current() => NativeMethods.GetForegroundWindow().ToInt64();

    /// <summary>Asks Windows to make the window the foreground one. Windows may refuse; the result says whether it did.</summary>
    public static bool TrySet(long hwnd) => hwnd != 0 && NativeMethods.SetForegroundWindow(new IntPtr(hwnd));
}
