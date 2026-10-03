using System.Runtime.InteropServices;
using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>Premultiplied BGRA pixels, top-down, as returned by the Windows Shell.</summary>
public sealed record ShellIconData(int Width, int Height, byte[] Pbgra32);

/// <summary>
/// The real Windows Shell icon for a path (the same image Explorer shows: folder, app icon, shortcut icon with its
/// overlay, file-type icon). Uses IShellItemImageFactory; no custom icon set. Call from an STA thread.
/// </summary>
public static class ShellIcons
{
    public static ShellIconData? TryGetIcon(string path, int sizePx)
    {
        var iid = NativeShell.IID_IShellItemImageFactory;
        if (NativeShell.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var factory) != 0 || factory is null)
        {
            return null;
        }

        IntPtr hbm = IntPtr.Zero;
        try
        {
            var size = new NativeShell.SIZE { cx = sizePx, cy = sizePx };
            if (factory.GetImage(size, NativeShell.SIIGBF_ICONONLY, out hbm) != 0 || hbm == IntPtr.Zero)
            {
                return null;
            }

            return ReadPixels(hbm);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (hbm != IntPtr.Zero)
            {
                NativeShell.DeleteObject(hbm);
            }

            Marshal.ReleaseComObject(factory);
        }
    }

    /// <summary>The name Explorer would show for the item (e.g. "Docker Desktop" for "Docker Desktop.lnk"), or null.</summary>
    public static string? TryGetDisplayName(string path)
    {
        var info = new NativeShell.SHFILEINFO { szDisplayName = string.Empty, szTypeName = string.Empty };
        var result = NativeShell.SHGetFileInfoW(path, 0, ref info, (uint)Marshal.SizeOf<NativeShell.SHFILEINFO>(), NativeShell.SHGFI_DISPLAYNAME);
        return result == IntPtr.Zero || string.IsNullOrWhiteSpace(info.szDisplayName) ? null : info.szDisplayName;
    }

    private static ShellIconData? ReadPixels(IntPtr hbm)
    {
        if (NativeShell.GetObjectW(hbm, Marshal.SizeOf<NativeShell.BITMAP>(), out var bm) == 0 || bm.bmWidth <= 0 || bm.bmHeight <= 0)
        {
            return null;
        }

        var width = bm.bmWidth;
        var height = bm.bmHeight;
        var bmi = new NativeShell.BITMAPINFO
        {
            bmiHeader = new NativeShell.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeShell.BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // negative = top-down rows
                biPlanes = 1,
                biBitCount = 32,
                biCompression = 0, // BI_RGB
            },
        };

        var pixels = new byte[width * height * 4];
        var hdc = NativeShell.GetDC(IntPtr.Zero);
        try
        {
            if (NativeShell.GetDIBits(hdc, hbm, 0, (uint)height, pixels, ref bmi, 0) == 0)
            {
                return null;
            }
        }
        finally
        {
            NativeShell.ReleaseDC(IntPtr.Zero, hdc);
        }

        // Some icons come back without an alpha channel (all alpha = 0 but visible colour): make them opaque.
        var anyAlpha = false;
        var anyColour = false;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            anyAlpha |= pixels[i + 3] != 0;
            anyColour |= (pixels[i] | pixels[i + 1] | pixels[i + 2]) != 0;
        }

        if (!anyAlpha && anyColour)
        {
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }

        return new ShellIconData(width, height, pixels);
    }
}
