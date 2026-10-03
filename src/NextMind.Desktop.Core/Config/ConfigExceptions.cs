namespace NextMind.Desktop.Core.Config;

/// <summary>The config text is not usable (malformed JSON, wrong shape, wrong types).</summary>
public sealed class ConfigFormatException : Exception
{
    public ConfigFormatException(string message) : base(message) { }

    public ConfigFormatException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The config was written by a newer build. We must read nothing into it and never overwrite it.</summary>
public sealed class ConfigTooNewException(int version)
    : Exception($"Config schema version {version} is newer than supported version {ConfigSchema.Current}.")
{
    public int Version { get; } = version;
}
