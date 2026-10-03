using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>Windows Known Folder API (SHGetKnownFolderPath). Nothing in the product assumes a user-name based path.</summary>
public static class KnownFolders
{
    public static readonly Guid Desktop = new("B4BFCC3A-DB2C-424C-B029-7FE99A87C641");
    public static readonly Guid PublicDesktop = new("C4AA340D-F20F-4863-AFEF-F87EF2E6BA25");
    public static readonly Guid Documents = new("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
    public static readonly Guid Pictures = new("33E28130-4E1E-4676-835A-98395C3BC3BB");
    public static readonly Guid LocalAppData = new("F1B32785-6FBA-4FCF-9D55-7B8E7F157091");

    public static string? TryGetPath(Guid folderId)
    {
        if (NativeMethods.SHGetKnownFolderPath(folderId, 0, IntPtr.Zero, out var ptr) != 0 || ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr);
        }
        finally
        {
            NativeMethods.CoTaskMemFree(ptr);
        }
    }
}
