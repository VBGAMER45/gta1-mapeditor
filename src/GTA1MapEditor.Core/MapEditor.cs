using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core;

/// <summary>
/// Clone-on-write editor for a <see cref="CmpMap"/>. The first time a tile
/// is touched, both its column and the targeted block are duplicated so
/// edits to it don't bleed into other tiles that originally shared the same
/// column or block. Subsequent edits to the same tile mutate in place.
/// </summary>
public sealed class MapEditor
{
    public CmpMap Map { get; }
    public bool IsDirty { get; private set; }

    /// <summary>Tile keys (x + y*256) whose column has already been detached.</summary>
    private readonly HashSet<int> _detached = new();

    public MapEditor(CmpMap map) { Map = map; }

    public void MarkDirty() => IsDirty = true;
    public void ClearDirty() => IsDirty = false;

    /// <summary>
    /// Return the BlockInfo at (x,y,z) ready to mutate. The first call for
    /// a given tile detaches its column and clones the block; later calls
    /// for the same tile return the same instance.
    /// </summary>
    public BlockInfo GetEditableBlock(int x, int y, int z)
    {
        if (x < 0 || x >= GameConfig.MapWidth || y < 0 || y >= GameConfig.MapHeight)
            throw new ArgumentOutOfRangeException(nameof(x));

        int tileKey = x + y * GameConfig.MapWidth;
        uint baseIdx = Map.Base[tileKey];
        int colIdx = (int)(baseIdx / 2);
        int airCount = Map.Columns[colIdx];
        int solidCount = GameConfig.MaxBlockHeight - airCount;
        if (z < 0 || z >= solidCount)
            throw new ArgumentOutOfRangeException(nameof(z), $"Tile ({x},{y}) has no block at z={z} (solid={solidCount}).");

        int slot = colIdx + solidCount - z;

        if (_detached.Contains(tileKey))
            return Map.Blocks[Map.Columns[slot]];

        // Detach: copy the column entries to the end of the columns array,
        // then clone the block at z and point the new column at the clone.
        int newColIdx = Map.Columns.Length;
        var newColumns = new ushort[Map.Columns.Length + 1 + solidCount];
        Map.Columns.AsSpan().CopyTo(newColumns);
        newColumns[newColIdx] = (ushort)airCount;
        for (int i = 1; i <= solidCount; i++)
            newColumns[newColIdx + i] = Map.Columns[colIdx + i];

        var cloned = Map.Blocks[Map.Columns[slot]].Clone();
        Map.Blocks.Add(cloned);
        int clonedIdx = Map.Blocks.Count - 1;
        newColumns[newColIdx + solidCount - z] = (ushort)clonedIdx;

        Map.Columns = newColumns;
        Map.Base[tileKey] = (uint)(newColIdx * 2);
        _detached.Add(tileKey);
        IsDirty = true;
        return cloned;
    }
}
