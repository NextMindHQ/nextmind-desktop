using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace NextMind.Desktop.Core.IO;

public sealed class RealFileSystem : IFileSystem
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool PathExists(string path)
    {
        // GetAttributes also sees dangling links and items the File/Directory.Exists helpers hide.
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

    public void WriteAllTextDurable(string path, string contents)
    {
        var bytes = Utf8NoBom.GetBytes(contents);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    public void ReplaceFile(string source, string destination, string backup)
        => File.Replace(source, destination, backup);

    public void MoveFile(string source, string destination)
        => File.Move(source, destination, overwrite: false);

    /// <summary>
    /// MoveFileEx with NO flags: a plain rename. Without MOVEFILE_COPY_ALLOWED it cannot degrade into copy+delete across
    /// volumes, and without MOVEFILE_REPLACE_EXISTING it cannot overwrite. Works for files and directories.
    /// </summary>
    public void MoveEntry(string source, string destination)
    {
        if (!MoveFileExW(ToExtended(source), ToExtended(destination), 0))
        {
            var error = Marshal.GetLastWin32Error();
            throw new IOException($"Could not rename '{source}' to '{destination}': {new Win32Exception(error).Message} (Win32 error {error}).", new Win32Exception(error));
        }
    }

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteEmptyDirectory(string path) => Directory.Delete(path, recursive: false);

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern)
        => Directory.EnumerateFiles(directory, searchPattern);

    public IEnumerable<string> EnumerateEntries(string directory)
        => Directory.EnumerateFileSystemEntries(directory);

    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);

    public long GetFileLength(string path) => new FileInfo(path).Length;

    private static string ToExtended(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            return full;
        }

        return full.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + full[2..] : @"\\?\" + full;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, uint dwFlags);
}
