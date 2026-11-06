using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Tests;

public sealed class WorkspaceProfileTests
{
    [Fact]
    public void Create_SnapshotsCurrentProfile_AndActivatesEmptyWorkspace()
    {
        var config = ConfigWithOneZone("Original");
        WorkspaceProfiles.EnsureInitialized(config);
        config.Zones[0].X = 123;

        var created = WorkspaceProfiles.Create(config, "Work");

        Assert.Equal(created.Id, config.ActiveProfileId);
        Assert.Equal("Work", created.Name);
        Assert.Empty(config.Zones);
        var original = Assert.Single(config.Profiles, profile => profile.Name == "Default");
        Assert.Equal(123, Assert.Single(original.Zones).X);
    }

    [Fact]
    public void Switch_SavesCurrentWorkspace_AndRestoresIndependentDeepClone()
    {
        var config = ConfigWithOneZone("Home");
        WorkspaceProfiles.EnsureInitialized(config);
        var homeId = config.ActiveProfileId;
        var work = WorkspaceProfiles.Create(config, "Work");
        config.Zones.Add(new ZoneConfig { Id = "work-zone", Title = "Work zone", X = 40 });

        Assert.True(WorkspaceProfiles.Switch(config, homeId));
        Assert.Equal("Home", Assert.Single(config.Zones).Title);
        config.Zones[0].Title = "Home changed";

        Assert.True(WorkspaceProfiles.Switch(config, work.Id));
        Assert.Equal("Work zone", Assert.Single(config.Zones).Title);
        Assert.Equal("Home changed", Assert.Single(config.Profiles.Single(profile => profile.Id == homeId).Zones).Title);
    }

    [Fact]
    public void Duplicate_DeepClonesReferences_ButDoesNotCreateSecondManagedOwner()
    {
        var config = ConfigWithOneZone("Mixed");
        config.Zones[0].Items =
        [
            new ItemConfig { Id = "ref", Kind = ItemKind.Reference, Path = @"C:\safe\file.txt", Name = "file" },
            new ItemConfig { Id = "managed", Kind = ItemKind.Managed, Path = @"C:\managed\owned.txt", Name = "owned", StoredName = "owned.txt" },
        ];
        WorkspaceProfiles.EnsureInitialized(config);

        var copy = WorkspaceProfiles.Duplicate(config, config.ActiveProfileId, "Copy");

        Assert.NotNull(copy);
        var copiedZone = Assert.Single(copy!.Zones);
        var copiedItem = Assert.Single(copiedZone.Items);
        Assert.Equal(ItemKind.Reference, copiedItem.Kind);
        Assert.NotEqual(config.Zones[0].Id, copiedZone.Id);
        Assert.NotEqual(config.Zones[0].Items[0].Id, copiedItem.Id);
        copiedZone.Title = "Changed copy";
        Assert.Equal("Mixed", config.Zones[0].Title);
    }

    [Fact]
    public void Delete_RefusesLastProfile_AndSwitchesAwayFromDeletedActiveProfile()
    {
        var config = ConfigWithOneZone("Home");
        WorkspaceProfiles.EnsureInitialized(config);
        var homeId = config.ActiveProfileId;

        Assert.False(WorkspaceProfiles.Delete(config, homeId));

        var work = WorkspaceProfiles.Create(config, "Work");
        Assert.True(WorkspaceProfiles.Delete(config, work.Id));
        Assert.Equal(homeId, config.ActiveProfileId);
        Assert.Equal("Home", Assert.Single(config.Zones).Title);
    }

    [Fact]
    public void Delete_RefusesProfileThatOwnsManagedItems()
    {
        var config = ConfigWithOneZone("Home");
        config.Zones[0].Items.Add(new ItemConfig
        {
            Id = "managed",
            Kind = ItemKind.Managed,
            Path = @"C:\managed\owned.txt",
            Name = "owned",
            StoredName = "owned.txt",
        });
        WorkspaceProfiles.EnsureInitialized(config);
        var ownedProfileId = config.ActiveProfileId;
        WorkspaceProfiles.Create(config, "Empty");

        Assert.False(WorkspaceProfiles.Delete(config, ownedProfileId));
        Assert.Contains(config.Profiles, profile => profile.Id == ownedProfileId);
    }

    [Fact]
    public void V3Migration_CreatesDefaultProfileWithoutLosingZones()
    {
        const string json = """
            {
              "schemaVersion": 3,
              "zones": [ { "id": "legacy", "title": "Legacy", "x": -200, "y": 30, "width": 320, "height": 220, "dpi": 144 } ]
            }
            """;

        var parsed = ConfigMigrator.Parse(json);

        Assert.True(parsed.Migrated);
        Assert.Equal(ConfigSchema.Current, parsed.Config.SchemaVersion);
        Assert.Equal("Legacy", Assert.Single(parsed.Config.Zones).Title);
        var profile = Assert.Single(parsed.Config.Profiles);
        Assert.Equal("Default", profile.Name);
        Assert.Equal("Legacy", Assert.Single(profile.Zones).Title);
        Assert.Equal(profile.Id, parsed.Config.ActiveProfileId);
    }

    private static AppConfig ConfigWithOneZone(string title) => new()
    {
        Zones = [new ZoneConfig { Id = "zone-1", Title = title, X = 10, Y = 20 }],
    };
}
