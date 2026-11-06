using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextMind.Desktop.Core.Config;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: NextMind.ReleaseVerifier <source-config> <migrated-config> <report-json>");
    return 2;
}

var sourceJson = File.ReadAllText(args[0]);
var originalSchema = ReadSchema(sourceJson);
var before = ConfigMigrator.Parse(sourceJson).Config;
var migratedJson = ConfigSerializer.Serialize(before);
File.WriteAllText(args[1], migratedJson, new UTF8Encoding(false));
var after = ConfigMigrator.Parse(File.ReadAllText(args[1])).Config;

var beforeCanonical = ConfigSerializer.Serialize(before);
var afterCanonical = ConfigSerializer.Serialize(after);
var report = new
{
    Source = Path.GetFullPath(args[0]),
    Migrated = Path.GetFullPath(args[1]),
    OriginalSchema = originalSchema,
    TargetSchema = after.SchemaVersion,
    SemanticMatch = beforeCanonical == afterCanonical,
    Before = Describe(before, beforeCanonical),
    After = Describe(after, afterCanonical),
};
File.WriteAllText(args[2], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
Console.WriteLine(JsonSerializer.Serialize(report));
return beforeCanonical == afterCanonical ? 0 : 1;

static int ReadSchema(string json)
{
    using var document = JsonDocument.Parse(json);
    return document.RootElement.TryGetProperty("schemaVersion", out var value) ? value.GetInt32() : 0;
}

static object Describe(AppConfig config, string canonical)
{
    var zones = config.Zones.Concat(config.Profiles.SelectMany(profile => profile.Zones)).ToList();
    var widgets = config.Widgets.Concat(config.Profiles.SelectMany(profile => profile.Widgets)).ToList();
    return new
    {
        config.SchemaVersion,
        config.ActiveProfileId,
        Profiles = config.Profiles.Count,
        Zones = zones.Count,
        ZoneItems = zones.Sum(zone => zone.Items.Count),
        Widgets = widgets.Count,
        QuickNotes = widgets.Count(widget => widget.Kind == WidgetKind.QuickNote),
        QuickNoteHashes = widgets.Where(widget => widget.Kind == WidgetKind.QuickNote)
            .Select(widget => Sha256(widget.NoteText)).OrderBy(value => value).ToArray(),
        GeometryHash = Sha256(string.Join("|", zones.Select(zone => $"Z:{zone.Id}:{zone.X}:{zone.Y}:{zone.Width}:{zone.Height}:{zone.Dpi}:{zone.Collapsed}:{zone.Hidden}")
            .Concat(widgets.Select(widget => $"W:{widget.Id}:{widget.X}:{widget.Y}:{widget.Width}:{widget.Height}:{widget.Dpi}:{widget.Collapsed}:{widget.Hidden}"))
            .OrderBy(value => value))),
        config.AutostartDecided,
        config.AutostartEnabled,
        CanonicalSHA256 = Sha256(canonical),
    };
}

static string Sha256(string value)
    => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
