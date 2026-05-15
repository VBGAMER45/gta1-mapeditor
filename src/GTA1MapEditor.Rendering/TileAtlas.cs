using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Decodes every 64×64 indexed tile from a G24 style into a single RGBA atlas
/// laid out as a 32-column grid. Each cell carries a 1-pixel edge-clamp
/// border so adjacent tiles don't bleed into each other when GPU mipmaps
/// average pixels at zoomed-out levels.
/// </summary>
public sealed class TileAtlas
{
    public const int TileSize = 64;
    public const int Padding = 1;             // pixels of edge padding per side
    public const int CellSize = TileSize + Padding * 2;  // 66
    public const int Columns = 32;

    public int TileCount { get; }
    public int Rows { get; }
    public int WidthPixels => Columns * CellSize;
    public int HeightPixels => Rows * CellSize;
    public byte[] Rgba { get; }

    public TileAtlas(int tileCount, int rows, byte[] rgba)
    {
        TileCount = tileCount;
        Rows = rows;
        Rgba = rgba;
    }

    /// <summary>
    /// Decode every tile from a G24 style into a single padded RGBA atlas.
    /// (CMP wall byte N -> atlas slot N-1, lid byte N -> atlas slot
    /// sideCount + N - 1, so atlas[0] is the first side tile — there's no
    /// reserved transparent slot.)
    /// </summary>
    public static TileAtlas Build(G24StyleData style)
    {
        int tileCount = style.TileData.Length / (TileSize * TileSize);
        var palette = style.PaletteData;

        return BuildFromTiles(tileCount, tile =>
        {
            // paletteIndices stores 4 CLUT entries per tile — one for each
            // possible block remap (TypeMapExt bits 4-5). We bake at remap=0
            // (Carnage3D StyleData.cpp:329 — `paletteIndices[4*tile + remap]`).
            int paletteSlot = 4 * tile;
            int clutIndex = paletteSlot < style.PaletteIndices.Length
                ? style.PaletteIndices[paletteSlot]
                : 0;

            // Tile pixels are stored as a 4-tile-wide page, not as flat
            // consecutive 4096-byte blocks. Each "block row" is 4 tiles ×
            // 64 rows = 16384 bytes, with each scanline spanning 256 pixels
            // (4 tiles). See Carnage3D StyleData.cpp:370-382. The 4-row
            // alignment padding in ComputeOffsets exists for this reason.
            const int RowStride = 4 * TileSize;            // 256 px per scanline
            const int BlockRowBytes = TileSize * RowStride; // 64 × 256 = 16384
            int blockX = tile % 4;
            int blockY = tile / 4;
            int tileBase = blockY * BlockRowBytes + blockX * TileSize;

            var rgba = new byte[TileSize * TileSize * 4];
            Span<byte> rgb = stackalloc byte[3];
            var paletteSpan = palette.AsSpan();

            for (int py = 0; py < TileSize; py++)
            {
                int srcRow = tileBase + py * RowStride;
                for (int px = 0; px < TileSize; px++)
                {
                    byte pixel = style.TileData[srcRow + px];
                    int dst = (py * TileSize + px) * 4;
                    if (pixel == 0) { rgba[dst + 3] = 0; continue; }
                    if (Palette.LookupColor(paletteSpan, clutIndex, pixel, rgb))
                    {
                        rgba[dst + 0] = rgb[0];
                        rgba[dst + 1] = rgb[1];
                        rgba[dst + 2] = rgb[2];
                        rgba[dst + 3] = 255;
                    }
                }
            }
            return rgba;
        });
    }

    /// <summary>
    /// Generic atlas builder. <paramref name="getTileRgba"/> is invoked for
    /// each tile index 0..<paramref name="tileCount"/>-1 and should return
    /// a freshly-allocated 64×64×4 = 16384-byte RGBA buffer (top-left
    /// origin), or null to leave that slot transparent. Padding around
    /// each cell uses the tile's own edge pixels so mipmap minification
    /// stays in-tile.
    /// </summary>
    public static TileAtlas BuildFromTiles(int tileCount, Func<int, byte[]?> getTileRgba)
    {
        int rows = (tileCount + Columns - 1) / Columns;
        int width = Columns * CellSize;
        int height = rows * CellSize;
        var rgba = new byte[width * height * 4];

        for (int tile = 0; tile < tileCount; tile++)
        {
            var tilePixels = getTileRgba(tile);
            if (tilePixels is null || tilePixels.Length < TileSize * TileSize * 4) continue;

            int cellX = (tile % Columns) * CellSize;
            int cellY = (tile / Columns) * CellSize;
            int innerX = cellX + Padding;
            int innerY = cellY + Padding;

            // Inner 64×64 tile content.
            int srcStride = TileSize * 4;
            for (int py = 0; py < TileSize; py++)
            {
                int dstOff = ((innerY + py) * width + innerX) * 4;
                Buffer.BlockCopy(tilePixels, py * srcStride, rgba, dstOff, srcStride);
            }

            // Edge-clamp padding ring: copy the tile's border rows/columns
            // into the surrounding 1-pixel margin so linear minification
            // at the cell boundary stays inside the tile's own colours.
            int stride = width * 4;
            int innerBase = (innerY * width + innerX) * 4;
            for (int px = 0; px < TileSize; px++)
            {
                Buffer.BlockCopy(rgba, innerBase + px * 4, rgba, ((innerY - 1) * width + innerX + px) * 4, 4);
                Buffer.BlockCopy(rgba, innerBase + (TileSize - 1) * stride + px * 4, rgba, ((innerY + TileSize) * width + innerX + px) * 4, 4);
            }
            for (int py = 0; py < TileSize; py++)
            {
                Buffer.BlockCopy(rgba, innerBase + py * stride, rgba, ((innerY + py) * width + innerX - 1) * 4, 4);
                Buffer.BlockCopy(rgba, innerBase + py * stride + (TileSize - 1) * 4, rgba, ((innerY + py) * width + innerX + TileSize) * 4, 4);
            }
            // Four corner pixels
            Buffer.BlockCopy(rgba, innerBase, rgba, ((innerY - 1) * width + innerX - 1) * 4, 4);
            Buffer.BlockCopy(rgba, innerBase + (TileSize - 1) * 4, rgba, ((innerY - 1) * width + innerX + TileSize) * 4, 4);
            Buffer.BlockCopy(rgba, innerBase + (TileSize - 1) * stride, rgba, ((innerY + TileSize) * width + innerX - 1) * 4, 4);
            Buffer.BlockCopy(rgba, innerBase + (TileSize - 1) * stride + (TileSize - 1) * 4, rgba, ((innerY + TileSize) * width + innerX + TileSize) * 4, 4);
        }

        return new TileAtlas(tileCount, rows, rgba);
    }

    /// <summary>UV rectangle (u0,v0,u1,v1) for the inner 64x64 region of tile <paramref name="tileIndex"/>.</summary>
    public (float u0, float v0, float u1, float v1) GetUv(int tileIndex)
    {
        int cellX = (tileIndex % Columns) * CellSize;
        int cellY = (tileIndex / Columns) * CellSize;
        int innerX = cellX + Padding;
        int innerY = cellY + Padding;
        float w = WidthPixels;
        float h = HeightPixels;
        return (innerX / w, innerY / h, (innerX + TileSize) / w, (innerY + TileSize) / h);
    }
}
