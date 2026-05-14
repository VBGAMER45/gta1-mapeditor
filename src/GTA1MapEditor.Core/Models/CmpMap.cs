namespace GTA1MapEditor.Core.Models;

public sealed class CmpHeader
{
    public uint Version { get; set; }
    public byte StyleNumber { get; set; }
    public byte SampleNumber { get; set; }
    public uint RouteSize { get; set; }
    public uint ObjectPosSize { get; set; }
    public uint ColumnSize { get; set; }
    public uint BlockSize { get; set; }
    public uint NavDataSize { get; set; }
}

/// <summary>
/// Fully-parsed CMP map. Blocks, columns and base are kept as raw arrays so
/// the renderer can consume them with minimal allocations; edit operations
/// rebuild them via CmpWriter.
/// </summary>
public sealed class CmpMap
{
    public CmpHeader Header { get; init; } = new();

    /// <summary>256*256 grid of column offsets into <see cref="Columns"/>. Mutable so clone-on-write edits can re-point a tile at a new column.</summary>
    public uint[] Base { get; set; } = new uint[GameConfig.MapWidth * GameConfig.MapHeight];

    /// <summary>Shared pool of unique block records. Editors append to this list when a block is cloned for per-tile editing.</summary>
    public List<BlockInfo> Blocks { get; init; } = new();

    /// <summary>
    /// Column data: [airCount, topBlockIdx, ..., groundBlockIdx]. Multiple
    /// tiles can point at the same column to dedupe storage. Mutable so the
    /// editor can append fresh columns when detaching a tile.
    /// </summary>
    public ushort[] Columns { get; set; } = Array.Empty<ushort>();

    public List<MapObject> Objects { get; } = new();
    public List<CarPosition> CarPositions { get; } = new();
    public List<MapRoute> Routes { get; } = new();
    public List<NavSector> NavSectors { get; } = new();
    public List<SpawnLocation> SpawnLocations { get; } = new();

    /// <summary>Block stack at (x,y) ordered ground → top.</summary>
    public List<BlockInfo> GetBlockStack(int x, int y)
    {
        var stack = new List<BlockInfo>();
        if (x < 0 || x >= GameConfig.MapWidth || y < 0 || y >= GameConfig.MapHeight) return stack;

        uint baseIdx = Base[x + y * GameConfig.MapWidth];
        int colIdx = (int)(baseIdx / 2);
        int airCount = Columns[colIdx];
        int solidCount = GameConfig.MaxBlockHeight - airCount;

        // Columns store top-down after the air count; reverse so stack[0] = ground.
        for (int z = 0; z < solidCount; z++)
        {
            int blockIdx = Columns[colIdx + solidCount - z];
            if (blockIdx < Blocks.Count)
                stack.Add(Blocks[blockIdx]);
        }
        return stack;
    }

    /// <summary>Block at (x,y,z) where z=0 is ground; null if out of range.</summary>
    public BlockInfo? GetBlockAt(int x, int y, int z)
    {
        if (x < 0 || x >= GameConfig.MapWidth || y < 0 || y >= GameConfig.MapHeight) return null;
        uint baseIdx = Base[x + y * GameConfig.MapWidth];
        int colIdx = (int)(baseIdx / 2);
        int airCount = Columns[colIdx];
        int solidCount = GameConfig.MaxBlockHeight - airCount;
        if (z < 0 || z >= solidCount) return null;
        int blockIdx = Columns[colIdx + solidCount - z];
        return blockIdx < Blocks.Count ? Blocks[blockIdx] : null;
    }
}
