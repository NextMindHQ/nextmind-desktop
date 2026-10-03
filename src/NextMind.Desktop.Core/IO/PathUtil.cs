namespace NextMind.Desktop.Core.IO;

public static class PathUtil
{
    /// <summary>Full path without trailing separators and without the <c>\\?\</c> long-path prefix.</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var p = path;
        if (p.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
        {
            p = @"\\" + p[8..];
        }
        else if (p.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            p = p[4..];
        }

        var full = Path.GetFullPath(p);
        var root = Path.GetPathRoot(full);
        if (!string.IsNullOrEmpty(root) && full.Length > root.Length)
        {
            full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return full;
    }

    /// <summary>True when <paramref name="path"/> equals <paramref name="root"/> or lives anywhere beneath it (case-insensitive, separator-aware).</summary>
    public static bool IsSameOrUnder(string path, string root)
    {
        var p = Normalize(path);
        var r = Normalize(root);

        if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = r.EndsWith(Path.DirectorySeparatorChar) ? r : r + Path.DirectorySeparatorChar;
        return p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
