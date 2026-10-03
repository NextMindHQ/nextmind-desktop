namespace NextMind.Desktop.Core.IO;

/// <summary>
/// The locations that product code and automated tests must never touch:
/// the user's real Desktop, Documents, Pictures and legacy OneDrive tree.
/// Resolved through the Windows known-folder API (via <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/>);
/// nothing here is a hard-coded user path.
/// </summary>
public static class ProtectedPaths
{
    public static IReadOnlyList<string> FromEnvironment()
    {
        var roots = new List<string>();

        void Add(string? p)
        {
            if (!string.IsNullOrWhiteSpace(p))
            {
                roots.Add(PathUtil.Normalize(p));
            }
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            // Conventional locations next to the known-folder answers, in case a redirect changes later.
            Add(Path.Combine(profile, "Desktop"));
            Add(Path.Combine(profile, "Documents"));
            Add(Path.Combine(profile, "Pictures"));
            Add(Path.Combine(profile, "OneDrive"));
        }

        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        Add(oneDrive);

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
