using System.Windows;

namespace NextMind.Desktop.App;

public sealed record ZoneItemDragPayload(string ZoneId, string ItemId);

/// <summary>
/// Drag of an item INSIDE NextMind Desktop (reorder within a zone, or move to another zone). The data object carries only a private
/// in-process format: no file list and no paths. No other program — Explorer included — can read it as files, so this drag can never
/// relocate or delete anything on disk; it only changes configuration. This is deliberately separate from the drop of real files.
/// </summary>
public static class ZoneItemDrag
{
    public const string Format = "NextMind.Desktop.ZoneItem.v1";

    /// <summary>The effect offered/accepted for internal drags (reordering is a "move" in the user's mind; it is configuration only).</summary>
    public const DragDropEffects InternalEffect = DragDropEffects.Move;

    public static void Begin(DependencyObject source, ZoneItemDragPayload payload)
    {
        var data = new DataObject();
        data.SetData(Format, payload);
        DragDrop.DoDragDrop(source, data, InternalEffect);
    }

    public static bool TryGet(IDataObject data, out ZoneItemDragPayload payload)
    {
        if (data.GetDataPresent(Format) && data.GetData(Format) is ZoneItemDragPayload found)
        {
            payload = found;
            return true;
        }

        payload = new ZoneItemDragPayload(string.Empty, string.Empty);
        return false;
    }
}
