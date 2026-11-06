using NextMind.Desktop.App;

namespace NextMind.Desktop.Tests;

public sealed class AppRunModeTests
{
    [Fact]
    public void PortableEditionIsIsolatedButRemainsAProductRun()
    {
        var mode = AppRunMode.Resolve(portableMode: true, configDirectoryOverride: null);

        Assert.True(mode.IsIsolated);
        Assert.False(mode.IsDevelopment);
    }

    [Fact]
    public void RedirectedConfigIsBothDevelopmentAndIsolated()
    {
        var mode = AppRunMode.Resolve(portableMode: false, configDirectoryOverride: "C:\\isolated");

        Assert.True(mode.IsIsolated);
        Assert.True(mode.IsDevelopment);
    }

    [Fact]
    public void InstalledEditionWithoutOverrideIsNeither()
    {
        var mode = AppRunMode.Resolve(portableMode: false, configDirectoryOverride: null);

        Assert.False(mode.IsIsolated);
        Assert.False(mode.IsDevelopment);
    }
}
