using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextMind.Desktop.Core.Config;

public sealed record ParsedConfig(AppConfig Config, bool Migrated);

/// <summary>
/// Versioned config reader. Each schema version gets one explicit step; unknown (newer) versions are refused.
/// v0 = the pre-release shape with short keys (<c>w</c>/<c>h</c>) and no id/dpi; kept so the migration path is exercised from day one.
/// </summary>
public static class ConfigMigrator
{
    public static ParsedConfig Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(
                json,
                documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException ex)
        {
            throw new ConfigFormatException("Config is not valid JSON.", ex);
        }

        if (node is not JsonObject root)
        {
            throw new ConfigFormatException("Config root must be a JSON object.");
        }

        var version = ReadInt(root, "schemaVersion", defaultValue: 0);

        if (version > ConfigSchema.Current)
        {
            throw new ConfigTooNewException(version);
        }

        if (version < 0)
        {
            throw new ConfigFormatException("Config schemaVersion is negative.");
        }

        var migrated = false;
        if (version < 1)
        {
            root = MigrateV0ToV1(root);
            migrated = true;
        }

        // v1 -> v2 only adds optional fields (zone items, Hidden, AutostartDecided); their defaults apply, nothing to rewrite.
        // v2 -> v3 adds item Kind/AddedAtUtc/IsFolder/StoredName, zone SortMode and the managed-moves gate. Existing items stay
        // REFERENCES (Kind defaults to Reference, the gate to off); AddedAtUtc gets a deterministic fallback in ConfigValidator.
        if (version < ConfigSchema.Current)
        {
            migrated = true;
        }

        AppConfig? config;
        try
        {
            config = root.Deserialize<AppConfig>(ConfigSerializer.Options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            throw new ConfigFormatException("Config has the wrong shape.", ex);
        }

        if (config is null)
        {
            throw new ConfigFormatException("Config is empty.");
        }

        config.SchemaVersion = ConfigSchema.Current;
        ConfigValidator.Normalize(config);
        return new ParsedConfig(config, migrated);
    }

    private static JsonObject MigrateV0ToV1(JsonObject v0)
    {
        var zones = new JsonArray();

        if (v0.TryGetPropertyValue("zones", out var zonesNode) && zonesNode is not null)
        {
            if (zonesNode is not JsonArray array)
            {
                throw new ConfigFormatException("'zones' must be an array.");
            }

            foreach (var item in array)
            {
                if (item is not JsonObject z)
                {
                    throw new ConfigFormatException("Every zone must be an object.");
                }

                zones.Add(new JsonObject
                {
                    ["id"] = Guid.NewGuid().ToString("D"),
                    ["title"] = ReadString(z, "title", ZoneLimits.DefaultTitle),
                    ["x"] = ReadInt(z, "x", 0),
                    ["y"] = ReadInt(z, "y", 0),
                    ["width"] = ReadInt(z, "w", 320),
                    ["height"] = ReadInt(z, "h", 220),
                    ["collapsed"] = ReadBool(z, "collapsed", false),
                    ["dpi"] = ZoneLimits.DefaultDpi,
                });
            }
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["zonesVisible"] = !ReadBool(v0, "hidden", false),
            ["zones"] = zones,
        };
    }

    private static int ReadInt(JsonObject o, string name, int defaultValue)
    {
        if (!o.TryGetPropertyValue(name, out var n) || n is null)
        {
            return defaultValue;
        }

        try
        {
            return n.GetValue<int>();
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or OverflowException)
        {
            throw new ConfigFormatException($"'{name}' must be an integer.", ex);
        }
    }

    private static bool ReadBool(JsonObject o, string name, bool defaultValue)
    {
        if (!o.TryGetPropertyValue(name, out var n) || n is null)
        {
            return defaultValue;
        }

        try
        {
            return n.GetValue<bool>();
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            throw new ConfigFormatException($"'{name}' must be true or false.", ex);
        }
    }

    private static string ReadString(JsonObject o, string name, string defaultValue)
    {
        if (!o.TryGetPropertyValue(name, out var n) || n is null)
        {
            return defaultValue;
        }

        try
        {
            return n.GetValue<string>();
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            throw new ConfigFormatException($"'{name}' must be a string.", ex);
        }
    }
}
