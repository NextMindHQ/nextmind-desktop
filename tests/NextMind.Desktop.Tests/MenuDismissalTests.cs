using System.Text.RegularExpressions;
using NextMind.Desktop.Core.Ui;

namespace NextMind.Desktop.Tests;

/// <summary>
/// Zones are never the foreground window (no activation by design). WPF closes a context menu through mouse capture and keyboard focus, and both
/// only work for the foreground window, so a menu opened from a zone stayed on screen after a click elsewhere or Alt+Tab. The fix takes the
/// foreground on the hidden message window while a menu is open and hands it back afterwards — but never by force.
/// </summary>
public class MenuForegroundPolicyTests
{
    private const long Anchor = 100;
    private const long Previous = 200;

    [Fact]
    public void ForegroundStillOurs_RestoresThePreviousWindow()
        => Assert.True(MenuForeground.ShouldRestore(Previous, Anchor, current: Anchor));

    [Fact]
    public void UserAlreadyActivatedAnotherWindow_IsNeverOverridden()
        => Assert.False(MenuForeground.ShouldRestore(Previous, Anchor, current: 300));

    [Fact]
    public void UserSwitchedBackToThePreviousWindow_NothingToDo()
        => Assert.False(MenuForeground.ShouldRestore(Previous, Anchor, current: Previous));

    [Fact]
    public void NoPreviousForeground_NothingToRestore()
        => Assert.False(MenuForeground.ShouldRestore(0, Anchor, current: Anchor));

    [Fact]
    public void PreviousWasTheAnchorItself_NothingToRestore()
        => Assert.False(MenuForeground.ShouldRestore(Anchor, Anchor, current: Anchor));

    [Fact]
    public void NoForegroundWindowAtAll_NothingToRestore()
        => Assert.False(MenuForeground.ShouldRestore(Previous, Anchor, current: 0));
}

public class MenuDismissalWiringTests
{
    private static string AppDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NextMind.Desktop.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repo root not found."), "src", "NextMind.Desktop.App");
    }

    [Fact]
    public void EveryContextMenuOpenedFromCode_PreparesTheForegroundFirst()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AppDirectory(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            foreach (Match open in Regex.Matches(text, @"\.IsOpen\s*=\s*true"))
            {
                var before = text[Math.Max(0, open.Index - 600)..open.Index];
                if (!before.Contains("PrepareMenuForDismissal(", StringComparison.Ordinal))
                {
                    offenders.Add(Path.GetFileName(file) + " @" + open.Index);
                }
            }
        }

        Assert.True(offenders.Count == 0, "ContextMenu opened without PrepareMenuForDismissal: " + string.Join(", ", offenders));
    }

    [Fact]
    public void TitleBarMenu_IsPreparedBeforeItOpens()
    {
        var text = File.ReadAllText(Path.Combine(AppDirectory(), "ZoneWindow.xaml.cs"));
        Assert.Matches(@"TitleBar\.ContextMenuOpening\s*\+=[^;]*PrepareMenuForDismissal\(", text);
    }
}
