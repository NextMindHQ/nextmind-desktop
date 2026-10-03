using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>Per-session single instance: a named mutex plus a broadcast "activate" message for the second launch.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\NextMind.Desktop.SingleInstance.v1";

    public static readonly uint ActivateMessage = NativeMethods.RegisterWindowMessageW("NextMind.Desktop.Activate.v1");

    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Returns the lock when this is the first instance, otherwise null.</summary>
    public static SingleInstance? TryAcquire(string? mutexName = null)
    {
        var mutex = new Mutex(initiallyOwned: true, mutexName ?? MutexName, out var createdNew);
        if (createdNew)
        {
            return new SingleInstance(mutex);
        }

        mutex.Dispose();
        return null;
    }

    /// <summary>Asks the running instance to show its zones.</summary>
    public static void SignalExisting()
        => NativeMethods.PostMessageW(NativeMethods.HWND_BROADCAST, ActivateMessage, IntPtr.Zero, IntPtr.Zero);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread any more; process exit releases it anyway.
        }

        _mutex.Dispose();
    }
}
