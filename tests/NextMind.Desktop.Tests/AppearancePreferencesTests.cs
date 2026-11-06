using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public sealed class AppearancePreferencesTests
{
    [Fact]
    public void Defaults_AreReadableAndResourceConscious()
    {
        var config = new AppConfig();

        Assert.Equal(5, ConfigSchema.Current);
        Assert.Equal(0.55, config.Appearance.GlowIntensity);
        Assert.Equal(0.90, config.Appearance.PanelOpacity);
        Assert.Equal(AppearanceAccent.ElectricBlue, config.Appearance.Accent);
        Assert.True(config.Appearance.AnimationsEnabled);
        Assert.False(config.Appearance.ResourceSaverMode);
    }

    [Fact]
    public void V4Migration_AddsDefaultAppearanceWithoutLosingWorkspace()
    {
        const string json = """
            {
              "schemaVersion": 4,
              "zones": [ { "id": "legacy", "title": "Development" } ]
            }
            """;

        var parsed = ConfigMigrator.Parse(json);

        Assert.True(parsed.Migrated);
        Assert.Equal(5, parsed.Config.SchemaVersion);
        Assert.Equal("Development", Assert.Single(parsed.Config.Zones).Title);
        Assert.Equal(AppearanceAccent.ElectricBlue, parsed.Config.Appearance.Accent);
    }

    [Fact]
    public void Normalize_ClampsAppearanceAndRepairsUnknownAccent()
    {
        var config = new AppConfig
        {
            Appearance = new AppearancePreferencesConfig
            {
                GlowIntensity = -4,
                PanelOpacity = 7,
                Accent = (AppearanceAccent)99,
                AnimationsEnabled = false,
                ResourceSaverMode = true,
            },
        };

        ConfigValidator.Normalize(config);

        Assert.Equal(0, config.Appearance.GlowIntensity);
        Assert.Equal(1, config.Appearance.PanelOpacity);
        Assert.Equal(AppearanceAccent.ElectricBlue, config.Appearance.Accent);
        Assert.False(config.Appearance.AnimationsEnabled);
        Assert.True(config.Appearance.ResourceSaverMode);
    }

    [Fact]
    public void Serialization_RoundTripsAppearance()
    {
        var config = new AppConfig
        {
            Appearance = new AppearancePreferencesConfig
            {
                GlowIntensity = 0.72,
                PanelOpacity = 0.81,
                Accent = AppearanceAccent.Violet,
                AnimationsEnabled = false,
                ResourceSaverMode = true,
            },
        };

        var restored = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config));

        Assert.Equal(0.72, restored.Appearance.GlowIntensity);
        Assert.Equal(0.81, restored.Appearance.PanelOpacity);
        Assert.Equal(AppearanceAccent.Violet, restored.Appearance.Accent);
        Assert.False(restored.Appearance.AnimationsEnabled);
        Assert.True(restored.Appearance.ResourceSaverMode);
    }

    [Fact]
    public void ProfileSwitch_DoesNotChangeGlobalAppearance()
    {
        var config = new AppConfig
        {
            Appearance = new AppearancePreferencesConfig { Accent = AppearanceAccent.Amber, GlowIntensity = 0.2 },
            Zones = [new ZoneConfig { Title = "Default" }],
        };
        WorkspaceProfiles.EnsureInitialized(config);
        var defaultId = config.ActiveProfileId;
        var work = WorkspaceProfiles.Create(config, "Work");

        Assert.True(WorkspaceProfiles.Switch(config, defaultId));
        Assert.True(WorkspaceProfiles.Switch(config, work.Id));

        Assert.Equal(AppearanceAccent.Amber, config.Appearance.Accent);
        Assert.Equal(0.2, config.Appearance.GlowIntensity);
    }
}
