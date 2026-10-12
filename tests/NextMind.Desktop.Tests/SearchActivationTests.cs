using System.Text.RegularExpressions;

namespace NextMind.Desktop.Tests;

/// <summary>
/// Static guards for the search-box activation (prototype B1). A zone is inert by design (no activation, never above normal windows).
/// Letting the search box take the keyboard is the one exception, so the exception must stay narrow, symmetric and timer-free.
/// </summary>
public class SearchActivationTests
{
    private static string ZoneWindowSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NextMind.Desktop.sln")))
        {
            dir = dir.Parent;
        }

        return File.ReadAllText(Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repo root not found."),
            "src", "NextMind.Desktop.App", "ZoneWindow.xaml.cs"));
    }

    private static int Count(string text, string pattern) => Regex.Matches(text, pattern).Count;

    [Fact]
    public void ActivationIsLifted_InExactlyOnePlace_AndRestored_InExactlyOnePlace()
    {
        var src = ZoneWindowSource();
        Assert.Equal(1, Count(src, @"SetNoActivate\(_hwnd,\s*false\)"));
        Assert.Equal(1, Count(src, @"SetNoActivate\(_hwnd,\s*true\)"));
    }

    [Fact]
    public void LosingFocus_ClosingAndCollapsing_AllMakeTheZoneInertAgain()
    {
        var src = ZoneWindowSource();
        Assert.Matches(@"Deactivated\s*\+=\s*\(_,\s*_\)\s*=>[\s\S]{0,600}?EndTextActivation\(restoreForeground:\s*false\)", src);
        Assert.Matches(@"private void Collapse\(RectPx expanded\)\s*\{\s*CloseSearch\(\)", src);
        Assert.Matches(@"else if \(!IsVisible\)\s*\{\s*CloseSearch\(\)", src);
    }

    [Fact]
    public void Zone_NeverActivatesItself_NorBecomesTopmost_NorUsesTimersForSearch()
    {
        var src = ZoneWindowSource();
        Assert.DoesNotContain("Topmost = true", src);
        Assert.DoesNotContain(" Activate()", src);
        Assert.DoesNotContain("DispatcherTimer", src);
    }

    [Fact]
    public void PreviousWindow_IsRestoredOnlyThroughThePolicy()
    {
        var src = ZoneWindowSource();
        Assert.Contains("MenuForeground.ShouldRestore(previous, _hwnd.ToInt64(), ForegroundWindow.Current())", src);
    }
}
