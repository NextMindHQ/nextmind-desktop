using System.Runtime.InteropServices;
using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

public sealed record TrayMenuItem(int Id, string Text, bool Enabled = true, bool IsSeparator = false, bool Checked = false, bool IsDefault = false)
{
    public static TrayMenuItem Separator { get; } = new(0, string.Empty, true, true, false, false);
}

/// <summary>Notification-area icon via Shell_NotifyIcon, driven by a <see cref="MessageWindow"/>. UI thread only.</summary>
public sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;

    private readonly MessageWindow _window;
    private readonly IntPtr _icon;
    private readonly bool _ownsIcon;
    private readonly string _tooltip;
    private bool _added;

    public TrayIcon(MessageWindow window, string tooltip, string? iconSourceExe)
    {
        _window = window;
        _tooltip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        (_icon, _ownsIcon) = LoadIcon(iconSourceExe);
    }

    /// <summary>Adds the icon. Safe to call again after Explorer restarts (it re-adds from scratch).</summary>
    public bool Add()
    {
        var data = NewData();
        data.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP;
        data.uCallbackMessage = MessageWindow.TrayCallbackMessage;
        data.hIcon = _icon;
        data.szTip = _tooltip;

        // After an Explorer restart a stale registration can make ADD fail; a delete-then-add is the documented recovery.
        _added = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref data);
        if (!_added)
        {
            var del = NewData();
            NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref del);
            _added = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref data);
        }

        return _added;
    }

    public void Remove()
    {
        if (!_added)
        {
            return;
        }

        var data = NewData();
        NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref data);
        _added = false;
    }

    /// <summary>Shows the native popup menu at the cursor and returns the chosen item id (0 = dismissed).</summary>
    public int ShowMenu(IReadOnlyList<TrayMenuItem> items)
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            foreach (var item in items)
            {
                if (item.IsSeparator)
                {
                    NativeMethods.AppendMenuW(menu, NativeMethods.MF_SEPARATOR, UIntPtr.Zero, null);
                    continue;
                }

                var flags = NativeMethods.MF_STRING
                    | (item.Enabled ? 0u : NativeMethods.MF_GRAYED | NativeMethods.MF_DISABLED)
                    | (item.Checked ? NativeMethods.MF_CHECKED : 0u)
                    | (item.IsDefault ? NativeMethods.MF_DEFAULT : 0u);
                NativeMethods.AppendMenuW(menu, flags, (UIntPtr)(uint)item.Id, item.Text);
            }

            NativeMethods.GetCursorPos(out var pt);

            // Required so the menu closes when the user clicks elsewhere (documented Shell_NotifyIcon behaviour).
            NativeMethods.SetForegroundWindow(_window.Handle);
            var cmd = NativeMethods.TrackPopupMenuEx(
                menu,
                NativeMethods.TPM_RETURNCMD | NativeMethods.TPM_NONOTIFY | NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_BOTTOMALIGN,
                pt.X, pt.Y, _window.Handle, IntPtr.Zero);
            NativeMethods.PostMessageW(_window.Handle, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);
            return (int)cmd;
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private NativeMethods.NOTIFYICONDATA NewData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = IconId,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private static (IntPtr Icon, bool Owned) LoadIcon(string? exePath)
    {
        if (!string.IsNullOrEmpty(exePath))
        {
            var small = new IntPtr[1];
            if (NativeMethods.ExtractIconExW(exePath, 0, null, small, 1) >= 1 && small[0] != IntPtr.Zero)
            {
                return (small[0], true);
            }
        }

        return (NativeMethods.LoadIconW(IntPtr.Zero, NativeMethods.IDI_APPLICATION), false);
    }

    public void Dispose()
    {
        Remove();
        if (_ownsIcon && _icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
        }
    }
}
