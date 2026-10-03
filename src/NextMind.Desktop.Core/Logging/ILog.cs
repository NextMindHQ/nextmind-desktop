using System.Text;

namespace NextMind.Desktop.Core.Logging;

public interface ILog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class NullLog : ILog
{
    public static readonly NullLog Instance = new();

    public void Info(string message) { }

    public void Warn(string message) { }

    public void Error(string message, Exception? exception = null) { }
}

/// <summary>
/// Tiny append-only diagnostic log in the app's own directory. Never throws. Single-generation rotation by rename
/// (the previous generation is overwritten by the move, never deleted separately). Logs events and failures only —
/// nothing per mouse-move — and never file contents or secrets.
/// </summary>
public sealed class FileLog : ILog
{
    private const long MaxBytes = 512 * 1024;
    private readonly string _path;
    private readonly object _gate = new();

    public FileLog(string directory)
    {
        _path = Path.Combine(directory, "desktop.log");
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch
        {
            // Logging must never take the app down.
        }
    }

    public string FilePath => _path;

    public void Info(string message) => Write("INFO ", message);

    public void Warn(string message) => Write("WARN ", message);

    public void Error(string message, Exception? exception = null)
        => Write("ERROR", exception is null ? message : $"{message} :: {exception.GetType().Name}: {exception.Message}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_gate)
            {
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(_path, _path + ".1", overwrite: true);
                }

                File.AppendAllText(_path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // Swallow: diagnostics are best-effort.
        }
    }
}
