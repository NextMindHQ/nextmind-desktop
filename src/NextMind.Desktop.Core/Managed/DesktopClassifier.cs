using NextMind.Desktop.Core.IO;

namespace NextMind.Desktop.Core.Managed;

/// <summary>The Desktop folders as reported by the OS (Known Folder API in production, fake temp folders in tests).</summary>
public interface IDesktopFolders
{
    /// <summary>The current user's Desktop folder.</summary>
    string UserDesktop { get; }

    /// <summary>The shared (all-users) Desktop folder, if any.</summary>
    string? PublicDesktop { get; }
}

public sealed record StaticDesktopFolders(string UserDesktop, string? PublicDesktop = null) : IDesktopFolders;

public enum ItemOrigin
{
    /// <summary>A loose item directly on the user's Desktop: the only origin that may become a managed item.</summary>
    UserDesktopItem,

    /// <summary>A loose item on the shared Desktop. Reference only: it is shared by all users and may need other permissions.</summary>
    PublicDesktopItem,

    /// <summary>Anywhere else (including items nested in Desktop subfolders and the Desktop folders themselves). Reference only.</summary>
    External,
}

public static class DesktopClassifier
{
    /// <summary>Classifies by exact parent folder: only DIRECT children of a Desktop folder count as Desktop items.</summary>
    public static ItemOrigin Classify(IDesktopFolders desktops, string path)
    {
        string full;
        try
        {
            full = PathUtil.Normalize(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ItemOrigin.External;
        }

        var parent = Path.GetDirectoryName(full);
        if (string.IsNullOrEmpty(parent))
        {
            return ItemOrigin.External;
        }

        if (SameFolder(parent, desktops.UserDesktop))
        {
            return ItemOrigin.UserDesktopItem;
        }

        if (!string.IsNullOrWhiteSpace(desktops.PublicDesktop) && SameFolder(parent, desktops.PublicDesktop))
        {
            return ItemOrigin.PublicDesktopItem;
        }

        return ItemOrigin.External;
    }

    private static bool SameFolder(string a, string b)
        => string.Equals(PathUtil.Normalize(a), PathUtil.Normalize(b), StringComparison.OrdinalIgnoreCase);
}
