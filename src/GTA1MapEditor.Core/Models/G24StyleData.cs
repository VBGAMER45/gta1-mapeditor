namespace GTA1MapEditor.Core.Models;

/// <summary>Fully-decoded G24 style file. Tile pixels stay packed; renderers slice them on demand.</summary>
public sealed class G24StyleData
{
    public G24Header Header { get; init; } = new();
    public G24SectionOffsets Offsets { get; init; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    public List<G24CarInfo> Cars { get; init; } = new();
    public List<G24SpriteInfo> Sprites { get; init; } = new();

    /// <summary>21 entries indexed by <see cref="SpriteCategory"/>.</summary>
    public ushort[] SpriteNumbers { get; init; } = Array.Empty<ushort>();

    public List<G24ObjectInfo> Objects { get; init; } = new();
    public List<G24AnimInfo> Anims { get; init; } = new();

    /// <summary>
    /// Raw paged CLUT region (header.ClutSize rounded up to 64 KiB pages).
    /// 64 CLUTs per page, byte layout `clutData[px*256 + page*65536 + slot*4 + (B,G,R,pad)]`.
    /// Use <see cref="Palette.LookupColor"/> for decoded RGB.
    /// </summary>
    public byte[] PaletteData { get; init; } = Array.Empty<byte>();

    public ushort[] PaletteIndices { get; init; } = Array.Empty<ushort>();

    /// <summary>Packed indexed pixels for every sprite, arranged in 256-wide pages.</summary>
    public byte[] SpriteGraphics { get; init; } = Array.Empty<byte>();

    /// <summary>Raw concatenated tile pixels: side tiles, then lid, then aux. 4096 bytes each (64×64 indexed).</summary>
    public byte[] TileData { get; init; } = Array.Empty<byte>();

    /// <summary>Number of 64×64 tiles that come before the lid tiles in <see cref="TileData"/>.</summary>
    public int SideTileCount => (int)(Header.SideSize / 4096);
    public int LidTileCount  => (int)(Header.LidSize / 4096);
    public int AuxTileCount  => (int)(Header.AuxSize / 4096);
}
