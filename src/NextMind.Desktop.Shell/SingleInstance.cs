using NextMind.Desktop.Shell.Native;

namespace NextMind.Desktop.Shell;

/// <summary>Per-session single instance: a named mutex plus a broadcast "activate" message for the second launch.</summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\NextMind.Desktop.SingleInstance.v1";
    private const string ActivateName = "NextMind.Desktop.Activate.v1";

    private static string _suffix = string.Empty;
    private static uint _activateMessage;

    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    /// <summary>Mutex and activate-message names for an isolation suffix ("" = the production names).</summary>
    public static (string Mutex, string Activate) NamesFor(string suffix) => (MutexName + suffix, ActivateName + suffix);

    /// <summary>
    /// Development/test instances use their own mutex and activate message, so they can neither be blocked by nor signal a production
    /// instance. Call once, before <see cref="TryAcquire"/> and before the first use of <see cref="ActivateMessage"/>.
    /// </summary>
    public static void UseIsolation(string suffix)
    {
        if (_activateMessage != 0)
        {
            throw new InvalidOperationException("Isolation must be configured before the activate message is first used.");
        }

        _suffix = suffix;
    }

    /// <summary>The registered window message that asks a running instance (of the same isolation) to come forward.</summary>
    public static uint ActivateMessage
        => _activateMessage != 0 ? _activateMessage : _activateMessage = NativeMethods.RegisterWindowMessageW(NamesFor(_suffix).Activate);

    /// <summary>Returns the lock when this is the first instance, otherwise null.</summary>
    public static SingleInstance? TryAcquire(string? mutexName = null)
    {
        var mutex = new Mutex(initiallyOwned: true, mutexName ?? NamesFor(_suffix).Mutex, out var createdNew);
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
