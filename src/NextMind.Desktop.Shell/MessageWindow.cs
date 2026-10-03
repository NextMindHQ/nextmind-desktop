using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

public enum TrayMouseEvent
{
    LeftClick,
    DoubleClick,
    RightClick,
}

/// <summary>
/// A hidden, never-shown top-level window that receives the system messages the app needs without any polling:
/// the tray callback, "TaskbarCreated" (Explorer restarted), WM_DISPLAYCHANGE and our own activate-request.
/// It is a real top-level window on purpose: message-only windows do not receive broadcast messages such as
/// TaskbarCreated. It never appears in Alt+Tab or the taskbar because it is never made visible.
/// Must be created and disposed on the UI thread (it relies on that thread's message loop).
/// </summary>
public sealed class MessageWindow : IDisposable
{
    public const uint TrayCallbackMessage = NativeMethods.WM_USER + 1;

    private readonly uint _taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
    private readonly NativeMethods.WndProc _wndProc; // kept in a field so the delegate is not collected
    private readonly string _className = "NextMind.Desktop.MessageWindow." + Guid.NewGuid().ToString("N");
    private readonly IntPtr _hInstance = NativeMethods.GetModuleHandleW(null);
    private bool _disposed;

    public MessageWindow()
    {
        _wndProc = WndProc;

        var wc = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = _hInstance,
            lpszClassName = _className,
        };

        if (NativeMethods.RegisterClassExW(ref wc) == 0)
        {
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }

        Handle = NativeMethods.CreateWindowExW(0, _className, "NextMind Desktop (message window)", NativeMethods.WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, _hInstance, IntPtr.Zero);
        if (Handle == IntPtr.Zero)
        {
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
    }

    public IntPtr Handle { get; private set; }

    /// <summary>Explorer (re)created the taskbar: re-add the tray icon and re-anchor zones.</summary>
    public event Action? TaskbarCreated;

    /// <summary>Monitor layout or resolution changed.</summary>
    public event Action? DisplayChanged;

    /// <summary>A second instance asked the running one to come forward.</summary>
    public event Action? ActivateRequested;

    public event Action<TrayMouseEvent>? TrayMouse;

    /// <summary>Handler exceptions are reported here instead of unwinding into native code.</summary>
    public event Action<Exception>? HandlerFailed;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == _taskbarCreatedMessage)
            {
                TaskbarCreated?.Invoke();
                return IntPtr.Zero;
            }

            if (msg == SingleInstance.ActivateMessage)
            {
                ActivateRequested?.Invoke();
                return IntPtr.Zero;
            }

            switch (msg)
            {
                case NativeMethods.WM_DISPLAYCHANGE:
                    DisplayChanged?.Invoke();
                    return IntPtr.Zero;

                case TrayCallbackMessage:
                    switch ((uint)(lParam.ToInt64() & 0xFFFF))
                    {
                        case NativeMethods.WM_LBUTTONUP:
                            TrayMouse?.Invoke(TrayMouseEvent.LeftClick);
                            break;
                        case NativeMethods.WM_LBUTTONDBLCLK:
                            TrayMouse?.Invoke(TrayMouseEvent.DoubleClick);
                            break;
                        case NativeMethods.WM_RBUTTONUP:
                        case NativeMethods.WM_CONTEXTMENU:
                            TrayMouse?.Invoke(TrayMouseEvent.RightClick);
                            break;
                    }

                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            HandlerFailed?.Invoke(ex);
        }

        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(Handle);
            Handle = IntPtr.Zero;
        }

        NativeMethods.UnregisterClassW(_className, _hInstance);
    }
}
