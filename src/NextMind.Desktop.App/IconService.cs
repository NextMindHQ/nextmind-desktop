using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Shell;

namespace NextMind.Desktop.App;

/// <summary>
/// Loads Shell icons and probes target existence on one background STA thread (Shell COM objects want STA, and a slow
/// or unreachable network path must never block the UI). The thread sleeps on a queue: no CPU while idle.
/// </summary>
public sealed class IconService : IDisposable
{
    private const int MaxCachedIcons = 512;

    private readonly BlockingCollection<(ItemViewModel Item, int SizePx)> _queue = [];
    private readonly ConcurrentDictionary<string, BitmapSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPathProbe _probe = new RealPathProbe();
    private readonly Dispatcher _dispatcher;
    private readonly ILog _log;
    private readonly Thread _thread;

    public IconService(Dispatcher dispatcher, ILog log)
    {
        _dispatcher = dispatcher;
        _log = log;
        _thread = new Thread(Run) { IsBackground = true, Name = "NextMind.IconLoader" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    /// <summary>Queues a (re)load: existence probe + icon. Safe to call again to re-check a missing item.</summary>
    public void Request(ItemViewModel item, int sizePx)
    {
        if (!_queue.IsAddingCompleted)
        {
            try
            {
                _queue.Add((item, sizePx));
            }
            catch (InvalidOperationException)
            {
                // Shutting down.
            }
        }
    }

    private void Run()
    {
        foreach (var (item, sizePx) in _queue.GetConsumingEnumerable())
        {
            try
            {
                var exists = _probe.Exists(item.Path);
                var isDirectory = exists && ShellActions.IsDirectory(item.Path);
                BitmapSource? icon = null;

                if (exists)
                {
                    var key = $"{item.Path}|{sizePx}";
                    if (!_cache.TryGetValue(key, out icon))
                    {
                        var data = ShellIcons.TryGetIcon(item.Path, sizePx);
                        if (data is not null)
                        {
                            icon = BitmapSource.Create(data.Width, data.Height, 96, 96, PixelFormats.Pbgra32, null, data.Pbgra32, data.Width * 4);
                            icon.Freeze();
                        }

                        if (_cache.Count < MaxCachedIcons)
                        {
                            _cache[key] = icon;
                        }
                    }
                }

                _dispatcher.BeginInvoke(DispatcherPriority.Background, () => item.Apply(exists, icon, isDirectory));
            }
            catch (Exception ex)
            {
                _log.Error($"Icon load failed for '{item.Name}'.", ex);
            }
        }
    }

    public void Dispose() => _queue.CompleteAdding();
}
