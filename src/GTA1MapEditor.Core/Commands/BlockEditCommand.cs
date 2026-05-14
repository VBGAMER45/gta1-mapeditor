using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core.Commands;

/// <summary>
/// Atomic edit of one tile's block fields. Captures the pre-edit snapshot of
/// the targeted block so undo can restore it without touching shared state.
/// </summary>
public sealed class BlockEditCommand : IEditCommand
{
    private readonly int _x, _y, _z;
    private readonly BlockSnapshot _before;
    private readonly BlockSnapshot _after;

    public string Description => $"Edit tile ({_x},{_y},{_z})";

    public BlockEditCommand(int x, int y, int z, BlockSnapshot before, BlockSnapshot after)
    {
        _x = x; _y = y; _z = z; _before = before; _after = after;
    }

    public static BlockSnapshot Capture(BlockInfo b) => new(
        b.TypeMap, b.TypeMapExt, b.Left, b.Right, b.Top, b.Bottom, b.Lid);

    public void Do(MapEditor editor)   => Apply(editor.GetEditableBlock(_x, _y, _z), _after);
    public void Undo(MapEditor editor) => Apply(editor.GetEditableBlock(_x, _y, _z), _before);

    private static void Apply(BlockInfo block, BlockSnapshot s)
    {
        block.TypeMap = s.TypeMap;
        block.TypeMapExt = s.TypeMapExt;
        block.Left = s.Left;
        block.Right = s.Right;
        block.Top = s.Top;
        block.Bottom = s.Bottom;
        block.Lid = s.Lid;
    }
}

public readonly record struct BlockSnapshot(
    ushort TypeMap, byte TypeMapExt,
    byte Left, byte Right, byte Top, byte Bottom, byte Lid);
