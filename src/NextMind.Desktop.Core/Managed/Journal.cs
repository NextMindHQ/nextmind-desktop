using System.Text.Json;
using NextMind.Desktop.Core.Config;
using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Logging;

namespace NextMind.Desktop.Core.Managed;

public enum JournalKind
{
    /// <summary>Desktop → managed storage.</summary>
    MoveIn,

    /// <summary>Managed storage → Desktop.</summary>
    MoveOut,
}

/// <summary>
/// What is true ON DISK when this state is recorded. A journal file exists only while an operation is incomplete;
/// COMPLETE is represented by the file being removed.
/// </summary>
public enum JournalState
{
    /// <summary>Intent recorded; no data has been touched.</summary>
    Prepared,

    /// <summary>The rename is about to happen / may have happened. Recovery decides by looking at source and destination.</summary>
    Moving,

    /// <summary>The rename is known to have succeeded. Config has not been committed yet.</summary>
    Moved,

    /// <summary>The config now reflects the move; only the journal clean-up is left.</summary>
    ConfigCommitted,
}

public sealed class JournalEntry
{
    public string OpId { get; set; } = Guid.NewGuid().ToString("N");

    public JournalKind Kind { get; set; }

    public JournalState State { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public string ZoneId { get; set; } = string.Empty;

    public string SourcePath { get; set; } = string.Empty;

    public string DestinationPath { get; set; } = string.Empty;

    /// <summary>Everything needed to rebuild the config entry if the app dies before committing it.</summary>
    public string StoredName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsFolder { get; set; }

    public DateTime AddedAtUtc { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public string? Note { get; set; }
}

public sealed record JournalProblem(string File, string Message);

public sealed record JournalLoadResult(IReadOnlyList<JournalEntry> Entries, IReadOnlyList<JournalProblem> Problems);

/// <summary>
/// One small JSON file per in-flight operation, written durably (temp + atomic replace, previous generation kept as .bak).
/// Unreadable journals are never deleted or guessed at: they are reported so the user/maintainer can look at them.
/// </summary>
public sealed class JournalStore(IFileSystem fs, string directory, ILog? log = null)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly ILog _log = log ?? NullLog.Instance;

    public string DirectoryPath => directory;

    private string PathFor(string opId) => Path.Combine(directory, opId + ".json");

    /// <summary>Durably records the entry. Throws on failure: callers must not proceed with a data change if this throws.</summary>
    public void Write(JournalEntry entry, DateTime nowUtc)
    {
        entry.UpdatedUtc = nowUtc;
        if (entry.CreatedUtc == default)
        {
            entry.CreatedUtc = nowUtc;
        }

        fs.CreateDirectory(directory);
        var final = PathFor(entry.OpId);
        var temp = final + ".tmp";
        var backup = final + ".bak";
        var json = JsonSerializer.Serialize(entry, Options);

        fs.WriteAllTextDurable(temp, json);
        if (fs.FileExists(final))
        {
            fs.ReplaceFile(temp, final, backup);
        }
        else
        {
            fs.MoveFile(temp, final);
        }
    }

    /// <summary>COMPLETE: removes the operation's journal files (the app's own housekeeping files).</summary>
    public void Complete(JournalEntry entry)
    {
        foreach (var file in new[] { PathFor(entry.OpId), PathFor(entry.OpId) + ".bak", PathFor(entry.OpId) + ".tmp" })
        {
            try
            {
                if (fs.FileExists(file))
                {
                    fs.DeleteFile(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn($"Could not remove journal file '{Path.GetFileName(file)}': {ex.Message}");
            }
        }
    }

    public JournalLoadResult LoadPending()
    {
        var entries = new List<JournalEntry>();
        var problems = new List<JournalProblem>();

        if (!fs.DirectoryExists(directory))
        {
            return new JournalLoadResult(entries, problems);
        }

        foreach (var file in fs.EnumerateFiles(directory, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var entry = TryRead(file) ?? TryRead(file + ".bak");
            if (entry is null)
            {
                problems.Add(new JournalProblem(Path.GetFileName(file), "The journal file is unreadable and was left untouched."));
                _log.Error($"Unreadable journal '{Path.GetFileName(file)}' left in place for inspection.");
                continue;
            }

            entries.Add(entry);
        }

        return new JournalLoadResult(entries.OrderBy(e => e.CreatedUtc).ToList(), problems);
    }

    private JournalEntry? TryRead(string file)
    {
        try
        {
            if (!fs.FileExists(file))
            {
                return null;
            }

            var entry = JsonSerializer.Deserialize<JournalEntry>(fs.ReadAllText(file), Options);
            return entry is { OpId.Length: > 0, ItemId.Length: > 0 } ? entry : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
