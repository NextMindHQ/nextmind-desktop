using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public class ConfigMigrationTests
{
    [Fact]
    public void V0_ShortKeys_AreMigratedToV1()
    {
        const string v0 = """
            { "hidden": true,
              "zones": [ { "title": "🦈 NextMind", "x": 50, "y": 60, "w": 400, "h": 250, "collapsed": true } ] }
            """;

        var parsed = ConfigMigrator.Parse(v0);

        Assert.True(parsed.Migrated);
        Assert.Equal(ConfigSchema.Current, parsed.Config.SchemaVersion);
        Assert.False(parsed.Config.ZonesVisible);
        var z = Assert.Single(parsed.Config.Zones);
        Assert.Equal("🦈 NextMind", z.Title);
        Assert.Equal((50, 60, 400, 250), (z.X, z.Y, z.Width, z.Height));
        Assert.True(z.Collapsed);
        Assert.False(string.IsNullOrWhiteSpace(z.Id));
        Assert.Equal(ZoneLimits.DefaultDpi, z.Dpi);
    }

    [Fact]
    public void MissingVersion_IsTreatedAsV0()
    {
        var parsed = ConfigMigrator.Parse("""{ "zones": [] }""");
        Assert.True(parsed.Migrated);
        Assert.Empty(parsed.Config.Zones);
    }

    [Fact]
    public void CurrentVersion_IsNotMigrated()
    {
        var parsed = ConfigMigrator.Parse("{ \"schemaVersion\": " + ConfigSchema.Current + ", \"zones\": [] }");
        Assert.False(parsed.Migrated);
    }

    [Fact]
    public void NewerVersion_IsRefused()
    {
        var ex = Assert.Throws<ConfigTooNewException>(() => ConfigMigrator.Parse("""{ "schemaVersion": 99 }"""));
        Assert.Equal(99, ex.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("{ \"schemaVersion\": \"one\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"zones\": \"nope\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"zones\": [ { \"width\": \"wide\" } ] }")]
    [InlineData("{ \"schemaVersion\": 1, \"zones\": [ ")]
    [InlineData("{ \"zones\": [ 42 ] }")]
    [InlineData("{ \"zones\": [ { \"w\": \"x\" } ] }")]
    [InlineData("{ \"schemaVersion\": -3 }")]
    public void Malformed_Throws_ConfigFormatException(string json)
    {
        Assert.Throws<ConfigFormatException>(() => ConfigMigrator.Parse(json));
    }

    [Fact]
    public void Truncated_RealisticFile_IsMalformed()
    {
        var full = ConfigSerializer.Serialize(new AppConfig { Zones = { new ZoneConfig { Title = "🦈 NextMind" } } });
        var truncated = full[..(full.Length / 2)];

        Assert.Throws<ConfigFormatException>(() => ConfigMigrator.Parse(truncated));
    }
}
