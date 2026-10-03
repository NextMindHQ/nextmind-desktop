using System.Text.RegularExpressions;

namespace NextMind.Desktop.Tests;

/// <summary>
/// Static guard for the M1 rule "no destructive filesystem operations": product code may only delete its own
/// temp config file, and only inside RealFileSystem. Anything new that deletes must be a conscious, reviewed change.
/// </summary>
public class NoDestructiveOperationsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NextMind.Desktop.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repo root (NextMind.Desktop.sln) not found.");
    }

    [Fact]
    public void ProductCode_OnlyDeletesViaRealFileSystem()
    {
        var offenders = new List<string>();
        var pattern = new Regex(@"\b(File|Directory|FileInfo|DirectoryInfo)\s*\.\s*Delete\s*\(|\.Delete\s*\(\s*(true|recursive)|SHFileOperation|DeleteFileW|RemoveDirectory|FOF_ALLOWUNDO|FO_DELETE");

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (pattern.IsMatch(text) && !file.EndsWith("RealFileSystem.cs", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(Path.GetRelativePath(RepoRoot(), file));
            }
        }

        Assert.True(offenders.Count == 0, "Unexpected delete calls in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ProductCode_NeverMovesOrCopiesFiles_ExceptTheAppsOwnConfigAndLog()
    {
        // Reference-only milestone: no File/Directory Move/Copy anywhere, other than the config store (RealFileSystem)
        // and the app's own log rotation. User data is never relocated.
        var pattern = new Regex(@"\b(File|Directory|FileInfo|DirectoryInfo)\s*\.\s*(Move|Copy|MoveTo|CopyTo)\s*\(|\.(MoveTo|CopyTo)\s*\(|SHFileOperation|IFileOperation|MoveFileEx|CopyFileEx");
        var allowed = new[] { "RealFileSystem.cs", Path.Combine("Logging", "ILog.cs") };
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            if (pattern.IsMatch(File.ReadAllText(file)) && !allowed.Any(a => file.EndsWith(a, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add(Path.GetRelativePath(RepoRoot(), file));
            }
        }

        Assert.True(offenders.Count == 0, "Unexpected move/copy calls in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void DropHandling_AnswersLinkOnly_NeverMove()
    {
        // A Move drop effect makes Explorer delete the source after the drop. Zones are reference-only: Link or None.
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "src", "NextMind.Desktop.App", "ZoneWindow.xaml.cs"));

        Assert.Contains("DragDropEffects.Link", text);
        Assert.DoesNotContain("DragDropEffects.Move", text);
        Assert.DoesNotContain("DragDropEffects.Copy", text);
        Assert.DoesNotContain("DragDropEffects.All", text);
    }

    [Fact]
    public void ItemRemoval_NeverTouchesTheRecycleBin_OrTheFilesystem()
    {
        var files = new[] { "ZoneWindow.xaml.cs", "ZoneManager.cs", "ZoneItemDrag.cs" }
            .Select(f => File.ReadAllText(Path.Combine(RepoRoot(), "src", "NextMind.Desktop.App", f)));

        foreach (var text in files)
        {
            Assert.DoesNotContain("RecycleBin", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("FOF_ALLOWUNDO", text);
            Assert.DoesNotContain("File.", text.Replace("ShellActions.", string.Empty).Replace("Profile.", string.Empty), StringComparison.Ordinal);
            Assert.DoesNotContain("Directory.", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RealFileSystem_HasNoRecursiveDelete_AndMoveNeverOverwrites()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "src", "NextMind.Desktop.Core", "IO", "RealFileSystem.cs"));

        // The one directory delete is non-recursive (it throws if the folder has content) and is used for the app's own empty storage folders.
        Assert.Single(Regex.Matches(text, @"Directory\.Delete\("));
        Assert.Contains("Directory.Delete(path, recursive: false)", text);
        Assert.DoesNotContain("recursive: true", text);
        Assert.Contains("overwrite: false", text);
        // MoveEntry is a plain rename: no COPY_ALLOWED (cross-volume copy+delete) and no REPLACE_EXISTING (overwrite).
        Assert.Contains("MoveFileExW(ToExtended(source), ToExtended(destination), 0)", text);
    }

    [Fact]
    public void OnlyManagedItemService_RelocatesUserData()
    {
        // MoveEntry(...) is the only call that changes where user data lives. Besides the filesystem implementations that define it,
        // exactly one class may call it.
        var definers = new[] { "IFileSystem.cs", "RealFileSystem.cs", "GuardedFileSystem.cs" };
        var callers = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            if (Regex.IsMatch(File.ReadAllText(file), @"\.MoveEntry\(") && !definers.Any(d => file.EndsWith(d, StringComparison.OrdinalIgnoreCase)))
            {
                callers.Add(Path.GetFileName(file));
            }
        }

        Assert.Equal(["ManagedItemService.cs"], callers);
    }

    [Fact]
    public void ManagedMoves_AreGatedByTheConfigFlag_AndOffByDefault()
    {
        Assert.False(new NextMind.Desktop.Core.Config.AppConfig().ManagedDesktopMovesEnabled);
        Assert.False(NextMind.Desktop.Core.Config.ConfigSerializer.Deserialize("{ \"schemaVersion\": 3 }").ManagedDesktopMovesEnabled);
    }

    [Fact]
    public void InternalReorderDrag_NeverCarriesFileData()
    {
        // The drag that reorders/moves items between zones uses a private data format only: no FileDrop, so no other program
        // (Explorer included) could interpret it as files and act on them.
        var path = Path.Combine(RepoRoot(), "src", "NextMind.Desktop.App", "ZoneItemDrag.cs");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);

        Assert.DoesNotContain("FileDrop", text);
        Assert.DoesNotContain("FileNameW", text);
        Assert.Contains("DoDragDrop", text);
    }
}
