using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Core.IO;

/// <summary>Read-only "does this path exist" check, kept separate from <see cref="IFileSystem"/> so product code can look at
/// user paths without any write capability.</summary>
public interface IPathProbe
{
    bool Exists(string path);
}

public sealed class RealPathProbe : IPathProbe
{
    public bool Exists(string path)
    {
        try
        {
            return File.Exists(path) || Directory.Exists(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}

public enum ItemAvailability
{
    Available,
    Missing,
}

public static class ItemProbe
{
    public static ItemAvailability Check(IPathProbe probe, ItemConfig item)
        => probe.Exists(item.Path) ? ItemAvailability.Available : ItemAvailability.Missing;
}
