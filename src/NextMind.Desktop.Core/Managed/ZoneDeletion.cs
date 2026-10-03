using NextMind.Desktop.Core.Config;

namespace NextMind.Desktop.Core.Managed;

public enum ZoneDeleteOutcome
{
    Deleted,

    /// <summary>A managed item could not be returned to the Desktop. The zone was NOT deleted.</summary>
    ReturnFailed,

    /// <summary>All managed items are back on the Desktop but the configuration could not be saved; the zone stays (now without them).</summary>
    ConfigNotSaved,

    NotFound,
}

public sealed record ZoneDeleteResult(ZoneDeleteOutcome Outcome, string Message, IReadOnlyList<ManagedResult> Returned);

/// <summary>
/// Deleting a zone removes configuration only. A zone that owns managed items can never be deleted while they would be orphaned in
/// managed storage: every one of them is returned to the Desktop first, and if any return fails the zone is kept.
/// </summary>
public static class ZoneDeletion
{
    public static int ManagedCount(ZoneConfig zone) => zone.Items.Count(i => i.Kind == ItemKind.Managed);

    public static ZoneDeleteResult Delete(AppConfig config, string zoneId, ManagedItemService service, Func<bool> commit)
    {
        var zone = ZoneCatalog.Find(config, zoneId);
        if (zone is null)
        {
            return new ZoneDeleteResult(ZoneDeleteOutcome.NotFound, "The zone does not exist.", []);
        }

        var returned = ManagedCount(zone) > 0 ? service.ReturnAllToDesktop(zone, commit) : [];
        var failed = returned.FirstOrDefault(r => !r.Success);
        if (failed is not null)
        {
            return new ZoneDeleteResult(ZoneDeleteOutcome.ReturnFailed, $"The zone was not deleted because an item could not be returned to the Desktop: {failed.Message}", returned);
        }

        var index = config.Zones.IndexOf(zone);
        ZoneCatalog.Delete(config, zone.Id);

        bool committed;
        try
        {
            committed = commit();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            committed = false;
        }

        if (!committed)
        {
            config.Zones.Insert(Math.Clamp(index, 0, config.Zones.Count), zone);
            return new ZoneDeleteResult(ZoneDeleteOutcome.ConfigNotSaved, "The configuration could not be saved, so the zone was kept.", returned);
        }

        return new ZoneDeleteResult(ZoneDeleteOutcome.Deleted, "Zone deleted (configuration only).", returned);
    }
}
