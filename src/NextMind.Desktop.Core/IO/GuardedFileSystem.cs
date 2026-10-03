namespace NextMind.Desktop.Core.IO;

public sealed class ProtectedPathException(string path, string root)
    : InvalidOperationException($"Refusing to touch protected location '{path}' (inside '{root}').");

public sealed class SandboxViolationException(string path, string allowedRoot)
    : InvalidOperationException($"Path '{path}' is outside the allowed root '{allowedRoot}'.");

/// <summary>
/// Decorator that confines an <see cref="IFileSystem"/> to one allowed root and refuses
/// anything under a protected root. Used for the app's own config directory (defence in depth)
/// and, more importantly, wraps every filesystem used by automated tests so that a wrong
/// configuration can never reach the real Desktop / Documents / Pictures.
/// </summary>
public sealed class GuardedFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly string _allowedRoot;
    private readonly IReadOnlyList<string> _protectedRoots;

    public GuardedFileSystem(IFileSystem inner, string allowedRoot, IEnumerable<string> protectedRoots)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _allowedRoot = PathUtil.Normalize(allowedRoot);
        _protectedRoots = protectedRoots.Select(PathUtil.Normalize).ToList();

        // The allowed root itself must not be, or live inside, a protected location.
        Check(_allowedRoot);
    }

    public string AllowedRoot => _allowedRoot;

    private string Check(string path)
    {
        var full = PathUtil.Normalize(path);

        foreach (var root in _protectedRoots)
        {
            if (PathUtil.IsSameOrUnder(full, root))
            {
                throw new ProtectedPathException(full, root);
            }
        }

        if (!PathUtil.IsSameOrUnder(full, _allowedRoot))
        {
            throw new SandboxViolationException(full, _allowedRoot);
        }

        return path;
    }

    public bool FileExists(string path) => _inner.FileExists(Check(path));

    public bool DirectoryExists(string path) => _inner.DirectoryExists(Check(path));

    public bool PathExists(string path) => _inner.PathExists(Check(path));

    public void CreateDirectory(string path) => _inner.CreateDirectory(Check(path));

    public string ReadAllText(string path) => _inner.ReadAllText(Check(path));

    public void WriteAllTextDurable(string path, string contents) => _inner.WriteAllTextDurable(Check(path), contents);

    public void ReplaceFile(string source, string destination, string backup)
        => _inner.ReplaceFile(Check(source), Check(destination), Check(backup));

    public void MoveFile(string source, string destination) => _inner.MoveFile(Check(source), Check(destination));

    public void MoveEntry(string source, string destination) => _inner.MoveEntry(Check(source), Check(destination));

    public void DeleteFile(string path) => _inner.DeleteFile(Check(path));

    public void DeleteEmptyDirectory(string path) => _inner.DeleteEmptyDirectory(Check(path));

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
        => _inner.EnumerateFiles(Check(directory), searchPattern);

    public IEnumerable<string> EnumerateEntries(string directory) => _inner.EnumerateEntries(Check(directory));

    public FileAttributes GetAttributes(string path) => _inner.GetAttributes(Check(path));

    public long GetFileLength(string path) => _inner.GetFileLength(Check(path));
}

/// <summary>
/// Production filesystem for managed items: allows exactly the listed roots (managed storage, journal, the user's Desktop)
/// and nothing else. Unlike <see cref="GuardedFileSystem"/> it does not deny the Desktop, because the Desktop is one of its roots.
/// </summary>
public sealed class ScopedFileSystem : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly IReadOnlyList<string> _roots;

    public ScopedFileSystem(IFileSystem inner, IEnumerable<string> allowedRoots)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _roots = allowedRoots.Select(PathUtil.Normalize).ToList();
        if (_roots.Count == 0)
        {
            throw new ArgumentException("At least one allowed root is required.", nameof(allowedRoots));
        }
    }

    private string Check(string path)
    {
        var full = PathUtil.Normalize(path);
        if (!_roots.Any(r => PathUtil.IsSameOrUnder(full, r)))
        {
            throw new SandboxViolationException(full, string.Join("; ", _roots));
        }

        return path;
    }

    public bool FileExists(string path) => _inner.FileExists(Check(path));

    public bool DirectoryExists(string path) => _inner.DirectoryExists(Check(path));

    public bool PathExists(string path) => _inner.PathExists(Check(path));

    public void CreateDirectory(string path) => _inner.CreateDirectory(Check(path));

    public string ReadAllText(string path) => _inner.ReadAllText(Check(path));

    public void WriteAllTextDurable(string path, string contents) => _inner.WriteAllTextDurable(Check(path), contents);

    public void ReplaceFile(string source, string destination, string backup)
        => _inner.ReplaceFile(Check(source), Check(destination), Check(backup));

    public void MoveFile(string source, string destination) => _inner.MoveFile(Check(source), Check(destination));

    public void MoveEntry(string source, string destination) => _inner.MoveEntry(Check(source), Check(destination));

    public void DeleteFile(string path) => _inner.DeleteFile(Check(path));

    public void DeleteEmptyDirectory(string path) => _inner.DeleteEmptyDirectory(Check(path));

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
        => _inner.EnumerateFiles(Check(directory), searchPattern);

    public IEnumerable<string> EnumerateEntries(string directory) => _inner.EnumerateEntries(Check(directory));

    public FileAttributes GetAttributes(string path) => _inner.GetAttributes(Check(path));

    public long GetFileLength(string path) => _inner.GetFileLength(Check(path));
}
