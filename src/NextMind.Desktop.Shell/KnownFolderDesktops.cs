using NextMind.Desktop.Core.Managed;

namespace NextMind.Desktop.Shell;

/// <summary>The user's Desktop and the Public Desktop, asked from Windows (Known Folder API) at the moment of use — never a hard-coded path.</summary>
public sealed class KnownFolderDesktops : IDesktopFolders
{
    public string UserDesktop
        => KnownFolders.TryGetPath(KnownFolders.Desktop)
           ?? throw new InvalidOperationException("Windows did not report the Desktop folder.");

    public string? PublicDesktop => KnownFolders.TryGetPath(KnownFolders.PublicDesktop);
}
