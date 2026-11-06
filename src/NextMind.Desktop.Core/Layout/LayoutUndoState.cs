using NextMind.Desktop.Core.Geometry;

namespace NextMind.Desktop.Core.Layout;

public readonly record struct LayoutMove(string ElementId, RectPx Before, RectPx After);

public sealed class LayoutUndoState
{
    private LayoutMove? _move;

    public bool CanUndo => _move is not null;

    public void Record(LayoutMove move)
    {
        if (move.Before != move.After)
        {
            _move = move;
        }
    }

    public LayoutMove? Take()
    {
        var move = _move;
        _move = null;
        return move;
    }
}
