namespace NextMind.Desktop.Core.Ui;

/// <summary>
/// While a zone context menu is open the app borrows the foreground (on a hidden window) so WPF's dismiss-on-outside-click works.
/// When the menu closes the previous window gets it back — only if nobody took it in the meantime. Window handles are passed as plain numbers.
/// </summary>
public static class MenuForeground
{
    /// <summary>True only when we still hold the foreground and there is a different window that had it before the menu opened.</summary>
    public static bool ShouldRestore(long previous, long anchor, long current)
        => previous != 0 && previous != anchor && current == anchor;
}
