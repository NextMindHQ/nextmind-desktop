using NextMind.Desktop.Core;

namespace NextMind.Desktop.Tests;

public class AppPathsTests
{
    [Fact]
    public void InstalledEdition_UsesExistingDesktopDataRoot()
    {
        var path = AppPaths.ResolveConfigDirectory(@"C:\Users\Owner\AppData\Local", null, portableMode: false);

        Assert.Equal(@"C:\Users\Owner\AppData\Local\NextMind\Desktop", path);
    }

    [Fact]
    public void PortableEdition_UsesSeparatePerUserDataRoot()
    {
        var path = AppPaths.ResolveConfigDirectory(@"C:\Users\Owner\AppData\Local", null, portableMode: true);

        Assert.Equal(@"C:\Users\Owner\AppData\Local\NextMind\Desktop-Portable", path);
        Assert.NotEqual(
            AppPaths.ResolveConfigDirectory(@"C:\Users\Owner\AppData\Local", null, portableMode: false),
            path);
    }

    [Fact]
    public void ExplicitTestOverride_WinsOverEditionMode()
    {
        var path = AppPaths.ResolveConfigDirectory(@"C:\Local", @"D:\Isolated Test", portableMode: true);

        Assert.Equal(Path.GetFullPath(@"D:\Isolated Test"), path);
    }
}
