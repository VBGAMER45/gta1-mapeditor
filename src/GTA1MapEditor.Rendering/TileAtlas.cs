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
    /// Decode every tile (side + lid + aux) into a single padded RGBA atlas.
    /// Tile 0 is left transparent because both lid 0 and side 0 mean
    /// "no texture" in the CMP block records.
    /// </summary>
    public static TileAtlas Build(G24StyleData style)
    {
        int tileCount = style.TileData.Length / (TileSize * TileSize);
        int rows = (tileCount + Columns - 1) / Columns;
        int width = Columns * CellSize;
        int height = rows * CellSize;
        var rgba = new byte[width * height * 4];

        var palette = style.PaletteData.AsSpan();
        Span<byte> rgb = stackalloc byte[3];

        for (int tile = 1; tile < tileCount; tile++) // tile 0 stays transparent
        {
            // paletteIndices stores 4 CLUT entries per tile — one for each
            // possible block remap (TypeMapExt bits 4-5). We bake the atlas
            // at remap=0 (the base palette); per-block remap variations are
            // a future TODO that would either need 4× atlas variants or a
            // palette-lookup fragment shader. (Source: Carnage3D
            // StyleData.cpp:329 — `paletteIndices[4 * tile + remap]`.)
            int paletteSlot = 4 * tile;
            int clutIndex = paletteSlot < style.PaletteIndices.Length
                ? style.PaletteIndices[paletteSlot]
                : 0;
            int srcBase = tile * TileSize * TileSize;
            int cellX = (tile % Columns) * CellSize;
            int cellY = (tile / Columns) * CellSize;
            int innerX = cellX + Padding;
            int innerY = cellY + Padding;

            // Inner 64x64 tile content.
            for (int py = 0; py < TileSize; py++)
            {
                int dstRow = (innerY + py) * width;
                for (int px = 0; px < TileSize; px++)
                {
                    byte pixel = style.TileData[srcBase + py * TileSize + px];
                    int dst = (dstRow + innerX + px) * 4;
                    if (pixel == 0) { rgba[dst + 3] = 0; continue; }
                    if (Palette.LookupColor(palette, clutIndex, pixel, rgb))
                    {
                        rgba[dst + 0] = rgb[0];
                        rgba[dst + 1] = rgb[1];
                        rgba[dst + 2] = rgb[2];
                        rgba[dst + 3] = 255;
                    }
                }
            }

            // Edge-clamp padding: copy the four border rows/columns of the
            // tile into the surrounding 1-pixel ring so linear minification
            // at the cell boundary stays inside the tile's own colours.
            int stride = width * 4;
            int innerBase = (innerY * width + innerX) * 4;
            // Top / bottom rows
            for (int px = 0; px < TileSize; px++)
            {
                Buffer.BlockCopy(rgba, innerBase + px * 4, rgba, ((innerY - 1) * width + innerX + px) * 4, 4);
                Buffer.BlockCopy(rgba, innerBase + (TileSize - 1) * stride + px * 4, rgba, ((innerY + TileSize) * width + innerX + px) * 4, 4);
            }
            // Left / right columns
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
