using System.Diagnostics;
using System.IO;
using NextMind.Desktop.Core.Logging;
using NextMind.Desktop.Core.Search;
using NextMind.Desktop.Core.Widgets;

namespace NextMind.Desktop.App;

public sealed record SearchAction(SearchEntry Entry, Action Execute);

public static class LocalSearchCatalog
{
    public static IReadOnlyList<SearchAction> Build(
        ZoneManager zones,
        WidgetManager widgets,
        Action createZone,
        Action openControlCenter,
        ILog log)
    {
        var actions = new List<SearchAction>
        {
            Command("command:create-zone", "Create Zone", "NextMind Desktop", ["new panel"], createZone),
            Command("command:control-center", "Open Control Center", "NextMind Desktop", ["settings manage"], openControlCenter),
            Command("command:show-zones", "Show All Zones", "NextMind Desktop", ["restore visible"], zones.ShowAll),
            Command("command:auto-arrange", "Auto-arrange Zones", "NextMind Desktop", ["layout organize"], zones.AutoArrange),
        };

        actions.AddRange(zones.Zones.Select(zone => new SearchAction(
            new SearchEntry("zone:" + zone.Id, zone.Title, "Zone", SearchEntryKind.Zone, ["show open workspace"]),
            () => zones.ShowZone(zone.Id))));

        actions.AddRange(WidgetCatalog.Definitions.Select(definition => new SearchAction(
            new SearchEntry("widget:add:" + definition.Kind, "Add " + definition.Title, definition.Description, SearchEntryKind.Widget, ["widget desktop"]),
            () => widgets.Add(definition.Kind))));

        actions.AddRange(widgets.Widgets.Where(widget => widget.Hidden).Select(widget =>
        {
            var definition = WidgetCatalog.Definitions.Single(candidate => candidate.Kind == widget.Kind);
            return new SearchAction(
                new SearchEntry("widget:show:" + widget.Id, "Show " + definition.Title, "Hidden widget", SearchEntryKind.Widget, ["restore desktop"]),
                () => widgets.Show(widget.Id));
        }));

        foreach (var path in StartMenuShortcuts(log))
        {
            var captured = path;
            actions.Add(new SearchAction(
                new SearchEntry("app:" + path, Path.GetFileNameWithoutExtension(path), "Application", SearchEntryKind.Application, ["start menu launch"]),
                () => Launch(captured, log)));
        }

        return actions;
    }

    private static SearchAction Command(string id, string title, string subtitle, IReadOnlyList<string> keywords, Action execute)
        => new(new SearchEntry(id, title, subtitle, SearchEntryKind.Command, keywords), execute);

    private static IEnumerable<string> StartMenuShortcuts(ILog log)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            IEnumerable<string> shortcuts;
            try
            {
                shortcuts = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Warn($"Search: could not enumerate Start menu '{root}': {ex.Message}");
                continue;
            }

            foreach (var shortcut in shortcuts)
            {
                if (seen.Add(shortcut))
                {
                    yield return shortcut;
                }
            }
        }
    }

    private static void Launch(string path, ILog log)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log.Error($"Search: could not launch '{path}'.", ex);
            ChoiceWindow.Inform("Could not open application", "Windows could not open this Start menu shortcut.");
        }
    }
}
