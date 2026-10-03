namespace NextMind.Desktop.Core.Startup;

/// <summary>
/// Bounded retry delays for things that may not exist yet right after logon (the desktop host window, the taskbar).
/// Event-driven recovery (TaskbarCreated) is the primary mechanism; this only covers "Explorer is up but not ready yet".
/// </summary>
public static class BackoffSchedule
{
    /// <summary>0.5 s, 1, 2, 4, 8, 15, 30 s — about a minute in total, then give up quietly.</summary>
    public static readonly IReadOnlyList<TimeSpan> Default =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
    ];

    /// <summary>Delay before retry number <paramref name="attempt"/> (0-based), or null when the limit is reached.</summary>
    public static TimeSpan? Next(int attempt) => attempt >= 0 && attempt < Default.Count ? Default[attempt] : null;

    public static TimeSpan Total => TimeSpan.FromTicks(Default.Sum(d => d.Ticks));
}
