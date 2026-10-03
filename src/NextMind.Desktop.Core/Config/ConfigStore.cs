using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Core.Logging;

namespace NextMind.Desktop.Core.Config;

public enum ConfigLoadStatus
{
    /// <summary>No usable file existed; a fresh default config was created in memory (not yet written).</summary>
    Created,

    /// <summary>Main file read successfully.</summary>
    Loaded,

    /// <summary>Main file missing or unusable; state restored from the previous generation (config.json.bak).</summary>
    RecoveredFromBackup,

    /// <summary>Main file missing or unusable; state restored from a complete leftover temp file (crash during save).</summary>
    RecoveredFromTemp,

    /// <summary>Nothing usable at all. The unreadable file was kept as config.corrupt-*.json and defaults are used.</summary>
    CorruptReplacedWithDefaults,

    /// <summary>File written by a newer build. Defaults are used for this run and the file is never overwritten.</summary>
    ReadOnlyNewerVersion,
}

public sealed record ConfigLoadResult(AppConfig Config, ConfigLoadStatus Status, string? Detail = null)
{
    public bool IsReadOnly => Status == ConfigLoadStatus.ReadOnlyNewerVersion;
}

/// <summary>
/// Durable config storage. Save = write temp (flushed) then atomic replace, keeping the previous generation as .bak.
/// Load = main, else leftover temp, else .bak; unusable files are preserved (never overwritten blindly).
/// </summary>
public sealed class ConfigStore
{
    public const string MainFileName = "config.json";
    public const string BackupFileName = "config.json.bak";
    public const string TempFileName = "config.json.tmp";

    private readonly IFileSystem _fs;
    private readonly ILog _log;
    private readonly Func<DateTime> _clock;
    private readonly object _gate = new();
    private bool _readOnly;

    public ConfigStore(IFileSystem fs, string directory, ILog? log = null, Func<DateTime>? clock = null)
    {
        _fs = fs ?? throw new ArgumentNullException(nameof(fs));
        DirectoryPath = directory ?? throw new ArgumentNullException(nameof(directory));
        _log = log ?? NullLog.Instance;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public string DirectoryPath { get; }

    public string MainPath => Path.Combine(DirectoryPath, MainFileName);

    public string BackupPath => Path.Combine(DirectoryPath, BackupFileName);

    public string TempPath => Path.Combine(DirectoryPath, TempFileName);

    public bool IsReadOnly => _readOnly;

    public ConfigLoadResult Load()
    {
        lock (_gate)
        {
            _fs.CreateDirectory(DirectoryPath);

            var mainExists = _fs.FileExists(MainPath);
            string? mainProblem = null;

            if (mainExists)
            {
                try
                {
                    var parsed = ConfigMigrator.Parse(_fs.ReadAllText(MainPath));
                    if (parsed.Migrated)
                    {
                        _log.Info("Config migrated in memory to the current schema; it will be rewritten on next save.");
                    }

                    return new ConfigLoadResult(parsed.Config, ConfigLoadStatus.Loaded);
                }
                catch (ConfigTooNewException ex)
                {
                    _readOnly = true;
                    _log.Warn(ex.Message + " Running read-only with defaults; the file will not be modified.");
                    return new ConfigLoadResult(NewDefault(), ConfigLoadStatus.ReadOnlyNewerVersion, ex.Message);
                }
                catch (Exception ex) when (ex is ConfigFormatException or IOException or UnauthorizedAccessException)
                {
                    mainProblem = ex.Message;
                    _log.Warn($"Main config unusable: {ex.Message}");
                }
            }

            string? quarantined = null;
            if (mainExists)
            {
                quarantined = Quarantine();
                if (quarantined is null)
                {
                    // The unusable file is still at the main path. Saving would rotate it into .bak and destroy the
                    // last good generation, so stay read-only for this run instead.
                    _readOnly = true;
                    _log.Error("Unusable config could not be preserved; running read-only to protect the backup generation.");
                }
            }

            // A complete temp file is the newest state if we crashed between "write temp" and "replace".
            var fromTemp = TryRead(TempPath);
            if (fromTemp is not null)
            {
                _log.Warn("Recovered config from leftover temp file.");
                return new ConfigLoadResult(fromTemp, ConfigLoadStatus.RecoveredFromTemp, mainProblem);
            }

            var fromBackup = TryRead(BackupPath);
            if (fromBackup is not null)
            {
                _log.Warn("Recovered config from backup generation.");
                return new ConfigLoadResult(fromBackup, ConfigLoadStatus.RecoveredFromBackup, mainProblem);
            }

            if (mainExists)
            {
                return new ConfigLoadResult(
                    NewDefault(),
                    ConfigLoadStatus.CorruptReplacedWithDefaults,
                    $"{mainProblem} (kept as '{quarantined}')");
            }

            return new ConfigLoadResult(NewDefault(), ConfigLoadStatus.Created);
        }
    }

    /// <summary>Atomically persists the config. Returns false (and writes nothing) when running read-only.</summary>
    public bool Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_gate)
        {
            if (_readOnly)
            {
                return false;
            }

            config.SchemaVersion = ConfigSchema.Current;
            ConfigValidator.Normalize(config);
            var json = ConfigSerializer.Serialize(config);

            _fs.CreateDirectory(DirectoryPath);

            try
            {
                _fs.WriteAllTextDurable(TempPath, json);

                if (_fs.FileExists(MainPath))
                {
                    _fs.ReplaceFile(TempPath, MainPath, BackupPath);
                }
                else
                {
                    _fs.MoveFile(TempPath, MainPath);
                }

                return true;
            }
            catch
            {
                // The previous main file is untouched by a failed temp write or failed replace; tidy our own temp and rethrow.
                TryDeleteTemp();
                throw;
            }
        }
    }

    private AppConfig? TryRead(string path)
    {
        try
        {
            if (!_fs.FileExists(path))
            {
                return null;
            }

            return ConfigMigrator.Parse(_fs.ReadAllText(path)).Config;
        }
        catch (Exception ex) when (ex is ConfigFormatException or ConfigTooNewException or IOException or UnauthorizedAccessException)
        {
            _log.Warn($"Ignoring unusable '{Path.GetFileName(path)}': {ex.Message}");
            return null;
        }
    }

    private string? Quarantine()
    {
        var name = $"config.corrupt-{_clock():yyyyMMddHHmmssfff}.json";
        var target = Path.Combine(DirectoryPath, name);
        try
        {
            _fs.MoveFile(MainPath, target);
            _log.Warn($"Unusable config preserved as '{name}'.");
            return name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error("Could not preserve the unusable config file.", ex);
            return null;
        }
    }

    private void TryDeleteTemp()
    {
        try
        {
            if (_fs.FileExists(TempPath))
            {
                _fs.DeleteFile(TempPath);
            }
        }
        catch
        {
            // Best effort; a stale temp is harmless and ignored/overwritten later.
        }
    }

    private static AppConfig NewDefault() => new();
}
