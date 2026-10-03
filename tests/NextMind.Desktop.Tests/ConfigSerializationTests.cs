using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public class ConfigSerializationTests
{
    private static AppConfig Sample() => new()
    {
        ZonesVisible = false,
        Zones =
        [
            new ZoneConfig { Id = "11111111-1111-1111-1111-111111111111", Title = "🦈 NextMind", X = -1920, Y = 40, Width = 480, Height = 300, Collapsed = true, Dpi = 144 },
            new ZoneConfig { Id = "22222222-2222-2222-2222-222222222222", Title = "Zażółć gęślą jaźń — Größe Ärger", X = 10, Y = 20, Width = 200, Height = 100, Collapsed = false, Dpi = 96 },
        ],
    };

    [Fact]
    public void RoundTrip_PreservesEveryField()
    {
        var json = ConfigSerializer.Serialize(Sample());
        var back = ConfigSerializer.Deserialize(json);

        Assert.Equal(ConfigSchema.Current, back.SchemaVersion);
        Assert.False(back.ZonesVisible);
        Assert.Equal(2, back.Zones.Count);

        var z = back.Zones[0];
        Assert.Equal("11111111-1111-1111-1111-111111111111", z.Id);
        Assert.Equal("🦈 NextMind", z.Title);
        Assert.Equal(-1920, z.X);
        Assert.Equal(40, z.Y);
        Assert.Equal(480, z.Width);
        Assert.Equal(300, z.Height);
        Assert.True(z.Collapsed);
        Assert.Equal(144, z.Dpi);
    }

    [Fact]
    public void Polish_AndGerman_AreWrittenReadable_AndEmojiSurvivesEscaped()
    {
        var json = ConfigSerializer.Serialize(Sample());

        Assert.Contains("Zażółć gęślą jaźń", json);
        Assert.Contains("Größe Ärger", json);
        Assert.DoesNotContain("\\u017C", json); // 'ż' is not escaped

        // System.Text.Json always escapes supplementary-plane characters (the shark) as a surrogate pair.
        // That is valid JSON and round-trips; the title comparison below proves it.
        Assert.Equal("🦈 NextMind", ConfigSerializer.Deserialize(json).Zones[0].Title);
    }

    [Fact]
    public void Keys_AreCamelCase_AndVersionIsWritten()
    {
        var json = ConfigSerializer.Serialize(Sample());

        Assert.Contains("\"schemaVersion\": " + ConfigSchema.Current, json);
        Assert.Contains("\"zonesVisible\"", json);
        Assert.Contains("\"collapsed\"", json);
    }

    [Fact]
    public void Deserialize_ToleratesCommentsAndTrailingCommas()
    {
        const string json = """
            {
              // hand edited
              "schemaVersion": 1,
              "zones": [ { "id": "a", "title": "T", "x": 1, "y": 2, "width": 300, "height": 200, }, ],
            }
            """;

        var cfg = ConfigSerializer.Deserialize(json);
        Assert.Single(cfg.Zones);
        Assert.Equal("T", cfg.Zones[0].Title);
    }

    [Fact]
    public void Validator_ClampsSizes_FixesIds_AndTitles()
    {
        var cfg = new AppConfig
        {
            Zones =
            [
                new ZoneConfig { Id = "dup", Title = "  ", Width = -5, Height = 0, Dpi = 0, X = int.MaxValue },
                new ZoneConfig { Id = "dup", Title = new string('x', 500), Width = 1_000_000, Height = 1 },
                new ZoneConfig { Id = "", Title = "ok" },
            ],
        };

        ConfigValidator.Normalize(cfg);

        Assert.Equal(3, cfg.Zones.Select(z => z.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(cfg.Zones, z => Assert.False(string.IsNullOrWhiteSpace(z.Id)));
        Assert.Equal(ZoneLimits.DefaultTitle, cfg.Zones[0].Title);
        Assert.Equal(ZoneLimits.MaxTitleLength, cfg.Zones[1].Title.Length);
        Assert.Equal(320, cfg.Zones[0].Width);
        Assert.Equal(ZoneLimits.MaxSize, cfg.Zones[1].Width);
        Assert.Equal(ZoneLimits.MinHeight, cfg.Zones[1].Height);
        Assert.Equal(ZoneLimits.DefaultDpi, cfg.Zones[0].Dpi);
        Assert.Equal(ZoneLimits.MaxCoordinate, cfg.Zones[0].X);
    }
}
