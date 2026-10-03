using NextMind.Desktop.Core.IO;

namespace NextMind.Desktop.Tests.Support;

/// <summary>
/// The ONLY way tests get a filesystem. A throw-away directory under the system temp folder, wrapped in a
/// <see cref="GuardedFileSystem"/> that refuses anything outside it and anything under the real Desktop,
/// Documents, Pictures or legacy OneDrive tree. A misconfigured test therefore fails loudly instead of
/// touching user data.
/// </summary>
public sealed class TempSandbox : IDisposable
{
    private const string Marker = "NextMindDesktopTests";

    public TempSandbox(string? leafName = null)
    {
        Root = Path.Combine(Path.GetTempPath(), Marker, Guid.NewGuid().ToString("N"));
        if (leafName is not null)
        {
            Root = Path.Combine(Root, leafName);
        }

        // Throws ProtectedPathException if the temp folder were ever redirected into a protected location.
        Fs = new GuardedFileSystem(new RealFileSystem(), Path.Combine(Path.GetTempPath(), Marker), ProtectedPaths.FromEnvironment());
        Fs.CreateDirectory(Root);
    }

    public string Root { get; }

    public IFileSystem Fs { get; }

    public string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    public void Dispose()
    {
        try
        {
            var top = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Marker));
            var full = Path.GetFullPath(Root);
            // Test-only cleanup of our own temp tree; never anywhere else.
            if (full.StartsWith(top + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var uniqueRoot = full[(top.Length + 1)..].Split(Path.DirectorySeparatorChar)[0];
                Directory.Delete(Path.Combine(top, uniqueRoot), recursive: true);
            }
        }
        catch
        {
            // Leftover temp files are harmless.
        }
    }
}

/// <summary>
/// FAIL FAST guard for every test that simulates a Desktop. A "fake desktop" must be a folder inside the test's temp sandbox;
/// if any path handed to a managed-items test is, or lies under, the machine's REAL Desktop / Public Desktop / Documents /
/// Pictures / OneDrive, the test aborts before doing anything.
/// </summary>
public static class RealDesktopGuard
{
    public static void AssertFake(string path)
    {
        var full = Path.GetFullPath(path);

        foreach (var real in ProtectedPaths.FromEnvironment())
        {
            if (PathUtil.IsSameOrUnder(full, real))
            {
                throw new InvalidOperationException($"FAIL FAST: test root '{full}' points at the real user folder '{real}'. Automated tests must only use a temp FAKE DESKTOP.");
            }
        }

        var temp = Path.GetFullPath(Path.GetTempPath());
        if (!PathUtil.IsSameOrUnder(full, temp))
        {
            throw new InvalidOperationException($"FAIL FAST: test root '{full}' is not inside the temp directory '{temp}'.");
        }
    }
}

/// <summary>Wraps a filesystem and throws an <see cref="IOException"/> the n-th time a named operation is invoked.</summary>
public sealed class FaultInjectingFileSystem(IFileSystem inner) : IFileSystem
{
    private readonly Dictionary<string, int> _failAfter = [];

    /// <summary>Make operation <paramref name="op"/> fail on its (skip+1)-th call.</summary>
    public void FailOn(string op, int skip = 0) => _failAfter[op] = skip;

    private void Gate(string op)
    {
        if (_failAfter.TryGetValue(op, out var left))
        {
            if (left <= 0)
            {
                _failAfter.Remove(op);
                throw new IOException($"Injected failure in {op}");
            }

            _failAfter[op] = left - 1;
        }
    }

    public bool FileExists(string path) => inner.FileExists(path);

    public bool DirectoryExists(string path) => inner.DirectoryExists(path);

    public bool PathExists(string path) => inner.PathExists(path);

    public void CreateDirectory(string path) { Gate(nameof(CreateDirectory)); inner.CreateDirectory(path); }

    public string ReadAllText(string path) { Gate(nameof(ReadAllText)); return inner.ReadAllText(path); }

    public void WriteAllTextDurable(string path, string contents) { Gate(nameof(WriteAllTextDurable)); inner.WriteAllTextDurable(path, contents); }

    public void ReplaceFile(string source, string destination, string backup) { Gate(nameof(ReplaceFile)); inner.ReplaceFile(source, destination, backup); }

    public void MoveFile(string source, string destination) { Gate(nameof(MoveFile)); inner.MoveFile(source, destination); }

    public void MoveEntry(string source, string destination) { Gate(nameof(MoveEntry)); inner.MoveEntry(source, destination); }

    public void DeleteFile(string path) { Gate(nameof(DeleteFile)); inner.DeleteFile(path); }

    public void DeleteEmptyDirectory(string path) { Gate(nameof(DeleteEmptyDirectory)); inner.DeleteEmptyDirectory(path); }

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern) => inner.EnumerateFiles(directory, searchPattern);

    public IEnumerable<string> EnumerateEntries(string directory) => inner.EnumerateEntries(directory);

    public FileAttributes GetAttributes(string path) => inner.GetAttributes(path);

    public long GetFileLength(string path) => inner.GetFileLength(path);
}
