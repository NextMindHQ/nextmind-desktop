using System.Text.Encodings.Web;
using System.Text.Json;

namespace NextMind.Desktop.Core.Config;

public static class ConfigSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Keep emoji and Polish/German letters readable in the file instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return JsonSerializer.Serialize(config, Options);
    }

    /// <summary>Parses, migrates and normalises. Throws <see cref="ConfigFormatException"/> or <see cref="ConfigTooNewException"/>.</summary>
    public static AppConfig Deserialize(string json) => ConfigMigrator.Parse(json).Config;
}
