using System.Diagnostics;
using NextMind.Desktop.Core.Managed;
using NextMind.Desktop.Tests.Support;

namespace NextMind.Desktop.Tests;

public class MoveAssessorTests
{
    private static MoveAssessor Assessor(ManagedFixture f, MoveAssessmentOptions? options = null)
        => new(f.Box.Fs, f.Paths.Root, options);

    /// <summary>Creates a directory junction (no admin needed). Returns false if the machine refuses, so the test can bail out loudly.</summary>
    private static bool TryMakeJunction(string link, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        p.WaitForExit(10_000);
        return p.ExitCode == 0 && Directory.Exists(link);
    }

    [Theory]
    [InlineData("Battlefield 6.lnk")]
    [InlineData("Strona.url")]
    [InlineData("Notatka.txt")]
    [InlineData("zdjęcie 🦈.png")]
    public void ShortcutsAndSmallOrdinaryFiles_AreAllowed_WithoutAsking(string name)
    {
        using var f = new ManagedFixture();
        var path = f.CreateDesktopFile(name, "small");

        var a = Assessor(f).Assess(path);

        Assert.Equal(MoveVerdict.Allow, a.Verdict);
        Assert.False(a.IsDirectory);
    }

    [Fact]
    public void LargeFile_NeedsConfirmation_ButAHugeShortcutNameDoesNot()
    {
        using var f = new ManagedFixture();
        var opts = new MoveAssessmentOptions { SmallFileBytes = 10 };
        var big = f.CreateDesktopFile("film.mkv", new string('x', 100));
        var lnk = f.CreateDesktopFile("tiny.lnk", new string('x', 100));

        var a = Assessor(f, opts).Assess(big);

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.Contains(MoveReason.LargeFile, a.Reasons);
        Assert.Equal(MoveVerdict.Allow, Assessor(f, opts).Assess(lnk).Verdict);
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("run.bat")]
    [InlineData("tool.ps1")]
    public void ProgramFiles_NeedConfirmation(string name)
    {
        using var f = new ManagedFixture();
        var a = Assessor(f).Assess(f.CreateDesktopFile(name));

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.Contains(MoveReason.Executable, a.Reasons);
    }

    [Fact]
    public void SmallCleanFolder_IsAllowed()
    {
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("Notatki", ("a.txt", "A"), ("sub\\b.txt", "B"));

        var a = Assessor(f).Assess(dir);

        Assert.Equal(MoveVerdict.Allow, a.Verdict);
        Assert.True(a.IsDirectory);
        Assert.True(a.EntryCount >= 3);
        Assert.False(a.Truncated);
    }

    [Fact]
    public void LargeFolder_NeedsConfirmation_WithAnApproximateSize()
    {
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("CyberSecurity", ("big1.bin", new string('x', 600)), ("big2.bin", new string('x', 600)));

        var a = Assessor(f, new MoveAssessmentOptions { LargeFolderBytes = 1000 }).Assess(dir);

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.Contains(MoveReason.LargeFolder, a.Reasons);
        Assert.Equal(1200, a.ApproxBytes);
        Assert.Contains("folder", a.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".git")]
    [InlineData("node_modules")]
    [InlineData(".venv")]
    [InlineData("bin")]
    [InlineData("obj")]
    public void ProjectLikeFolders_NeedConfirmation_EvenWhenSmall(string marker)
    {
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("repo", ("README.md", "r"));
        f.Box.Fs.CreateDirectory(Path.Combine(dir, marker));

        var a = Assessor(f).Assess(dir);

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.Contains(MoveReason.ProjectFolder, a.Reasons);
    }

    [Fact]
    public void ProjectMarkerNestedDeeper_IsStillDetected()
    {
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("workspace", ("a\\b\\c.txt", "c"));
        f.Box.Fs.CreateDirectory(Path.Combine(dir, "a", "b", "node_modules"));

        Assert.Contains(MoveReason.ProjectFolder, Assessor(f).Assess(dir).Reasons);
    }

    [Fact]
    public void FolderWithManyEntries_NeedsConfirmation()
    {
        using var f = new ManagedFixture();
        var files = Enumerable.Range(0, 12).Select(i => ($"f{i}.txt", "x")).ToArray();
        var dir = f.CreateDesktopFolder("many", files);

        var a = Assessor(f, new MoveAssessmentOptions { ConfirmEntryCount = 10 }).Assess(dir);

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.Contains(MoveReason.TooManyEntries, a.Reasons);
    }

    [Fact]
    public void ScanIsBounded_ABigTreeIsReportedAsUnknownSize_NotScannedForever()
    {
        using var f = new ManagedFixture();
        var files = Enumerable.Range(0, 30).Select(i => ($"f{i}.txt", "x")).ToArray();
        var dir = f.CreateDesktopFolder("huge", files);

        var a = Assessor(f, new MoveAssessmentOptions { MaxEntriesScanned = 10 }).Assess(dir);

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.True(a.Truncated);
        Assert.Contains(MoveReason.SizeUnknown, a.Reasons);
        Assert.True(a.EntryCount <= 10);
        Assert.Contains("not fully scanned", a.Message);
    }

    [Fact]
    public void CancelledScan_ReportsUnknownSize()
    {
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("x", ("a.txt", "a"), ("b.txt", "b"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var a = Assessor(f).Assess(dir, cts.Token);

        Assert.Equal(MoveVerdict.Confirm, a.Verdict);
        Assert.Contains(MoveReason.SizeUnknown, a.Reasons);
    }

    [Fact]
    public void Junction_IsRejected()
    {
        using var f = new ManagedFixture();
        var target = f.Box.Combine("junction-target");
        f.Box.Fs.CreateDirectory(target);
        var link = f.OnDesktop("Skrót do folderu");
        Assert.True(TryMakeJunction(link, target), "could not create a junction for the test");

        var a = Assessor(f).Assess(link);

        Assert.Equal(MoveVerdict.Reject, a.Verdict);
        Assert.Contains(MoveReason.ReparsePoint, a.Reasons);
        Assert.True(Directory.Exists(target)); // the target of the link was never touched
    }

    [Fact]
    public void FolderContainingAJunction_IsRejected()
    {
        using var f = new ManagedFixture();
        var target = f.Box.Combine("junction-target");
        f.Box.Fs.CreateDirectory(target);
        var dir = f.CreateDesktopFolder("with-link", ("a.txt", "a"));
        Assert.True(TryMakeJunction(Path.Combine(dir, "inner-link"), target), "could not create a junction for the test");

        var a = Assessor(f).Assess(dir);

        Assert.Equal(MoveVerdict.Reject, a.Verdict);
        Assert.Contains(MoveReason.ContainsReparsePoint, a.Reasons);
        Assert.Contains("inner-link", a.Message);
    }

    [Fact]
    public void SystemAndHiddenItems_AreRejected()
    {
        using var f = new ManagedFixture();
        var ini = f.CreateDesktopFile("desktop.ini", "[.ShellClassInfo]");
        File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
        var hidden = f.CreateDesktopFile("secret.txt", "h");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        Assert.Equal(MoveReason.SystemOrHidden, Assert.Single(Assessor(f).Assess(ini).Reasons));
        Assert.Equal(MoveVerdict.Reject, Assessor(f).Assess(hidden).Verdict);
    }

    [Fact]
    public void ItemInsideManagedStorage_IsRejected()
    {
        using var f = new ManagedFixture();
        f.CreateDesktopFile("x.lnk");
        var moved = f.Service.MoveIntoZone(f.Zone, f.OnDesktop("x.lnk"), null, f.Commit);

        Assert.Equal(MoveReason.InsideManagedStorage, Assert.Single(Assessor(f).Assess(moved.Item!.Path).Reasons));
    }

    [Fact]
    public void MissingItem_IsRejected()
    {
        using var f = new ManagedFixture();
        Assert.Equal(MoveReason.NotFound, Assert.Single(Assessor(f).Assess(f.OnDesktop("ghost.lnk")).Reasons));
    }

    [Theory]
    [InlineData("C:\\a\\trailing.")]
    [InlineData("C:\\a\\trailing ")]
    public void OddNames_AreRejected(string path)
        => Assert.Equal(MoveVerdict.Reject, new MoveAssessor(new TempSandbox().Fs, "C:\\none").Assess(path).Verdict);

    [Fact]
    public void FormatSize_IsHumanReadable()
    {
        Assert.Equal("512 B", MoveAssessor.FormatSize(512));
        Assert.Equal("2 KB", MoveAssessor.FormatSize(2048));
        Assert.Equal("300 MB", MoveAssessor.FormatSize(300L * 1024 * 1024));
        Assert.Equal("7.4 GB", MoveAssessor.FormatSize((long)(7.4 * 1024 * 1024 * 1024)));
    }

    [Fact]
    public void RealMoveOfALargeFolder_IsStillAnInstantRename_AndNothingIsCopied()
    {
        // The confirmation exists for the user's benefit (their data's location changes), not because a rename is slow.
        using var f = new ManagedFixture();
        var dir = f.CreateDesktopFolder("Large", ("data.bin", new string('x', 5000)));
        var assessment = Assessor(f, new MoveAssessmentOptions { LargeFolderBytes = 100 }).Assess(dir);
        Assert.Equal(MoveVerdict.Confirm, assessment.Verdict);

        var sw = Stopwatch.StartNew();
        var r = f.Service.MoveIntoZone(f.Zone, dir, null, f.Commit);

        Assert.True(r.Success);
        Assert.True(sw.ElapsedMilliseconds < 2000);
        Assert.Equal(5000, new FileInfo(Path.Combine(r.Item!.Path, "data.bin")).Length);
    }
}
