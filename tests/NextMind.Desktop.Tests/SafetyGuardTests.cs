using NextMind.Desktop.Core.IO;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

/// <summary>Proves the test infrastructure itself cannot reach real user data.</summary>
public class SafetyGuardTests
{
    [Fact]
    public void TempSandbox_Root_IsNotInsideAnyProtectedLocation()
    {
        using var box = new TempSandbox();

        foreach (var protectedRoot in ProtectedPaths.FromEnvironment())
        {
            Assert.False(PathUtil.IsSameOrUnder(box.Root, protectedRoot), $"{box.Root} is under {protectedRoot}");
        }
    }

    [Fact]
    public void ProtectedPaths_ContainDesktop_Documents_Pictures_AndPublicDesktop()
    {
        var roots = ProtectedPaths.FromEnvironment();

        Assert.Contains(PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)), roots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)), roots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)), roots, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(PathUtil.Normalize(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)), roots, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guard_RefusesTheRealDesktop_EvenIfConfiguredAsAllowedRoot()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        Assert.Throws<ProtectedPathException>(
            () => new GuardedFileSystem(new RealFileSystem(), desktop, ProtectedPaths.FromEnvironment()));
    }

    [Fact]
    public void Guard_RefusesChildOfRealDesktop_AsAllowedRoot()
    {
        var child = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NextMindTestWouldBeHere");

        Assert.Throws<ProtectedPathException>(
            () => new GuardedFileSystem(new RealFileSystem(), child, ProtectedPaths.FromEnvironment()));
    }

    [Fact]
    public void Guard_RefusesPaths_OutsideTheSandbox()
    {
        using var box = new TempSandbox();
        var outside = Path.Combine(Path.GetTempPath(), "definitely-not-the-sandbox.txt");

        Assert.Throws<SandboxViolationException>(() => box.Fs.WriteAllTextDurable(outside, "x"));
        Assert.Throws<SandboxViolationException>(() => box.Fs.FileExists(outside));
    }

    [Fact]
    public void Guard_RefusesProtectedPaths_OnEveryOperation()
    {
        using var box = new TempSandbox();
        var protectedDir = Path.Combine(box.Root, "pretend-this-is-the-desktop");
        var fs = new GuardedFileSystem(new RealFileSystem(), box.Root, [protectedDir]);
        var f = Path.Combine(protectedDir, "a.txt");

        Assert.Throws<ProtectedPathException>(() => fs.ReadAllText(f));
        Assert.Throws<ProtectedPathException>(() => fs.FileExists(f));
        Assert.Throws<ProtectedPathException>(() => fs.WriteAllTextDurable(f, "x"));
        Assert.Throws<ProtectedPathException>(() => fs.MoveFile(f, f + "2"));
        Assert.Throws<ProtectedPathException>(() => fs.DeleteFile(f));
        Assert.Throws<ProtectedPathException>(() => fs.ReplaceFile(f, f, f));
        Assert.Throws<ProtectedPathException>(() => fs.CreateDirectory(f));
        Assert.Throws<ProtectedPathException>(() => fs.EnumerateFiles(protectedDir, "*"));
        Assert.False(Directory.Exists(protectedDir)); // nothing was created behind the guard's back

        // ...while the rest of the sandbox stays usable.
        fs.WriteAllTextDurable(Path.Combine(box.Root, "fine.txt"), "ok");
    }

    [Fact]
    public void Guard_DoesNotBeFooledBy_DotDotTraversal_CaseOrSiblingPrefixes()
    {
        using var box = new TempSandbox();

        var traversal = Path.Combine(box.Root, "..", "..", "escape.txt");
        Assert.Throws<SandboxViolationException>(() => box.Fs.WriteAllTextDurable(traversal, "x"));

        var sibling = box.Root + "-sibling";
        Assert.False(PathUtil.IsSameOrUnder(sibling, box.Root));

        Assert.True(PathUtil.IsSameOrUnder(box.Root.ToUpperInvariant() + @"\a\b", box.Root));
    }

    [Fact]
    public void LongPathPrefix_IsNormalisedAway()
    {
        Assert.Equal(@"C:\a\b", PathUtil.Normalize(@"\\?\C:\a\b\"));
        Assert.Equal(@"\\server\share\x", PathUtil.Normalize(@"\\?\UNC\server\share\x"));
    }
}
