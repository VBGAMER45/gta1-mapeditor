using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.Core;

/// <summary>
/// Decodes G24 sprites to RGBA pixel buffers. Sprite graphics live in 256-wide
/// pages, addressed via <see cref="G24SpriteInfo.PageNumber"/>,
/// <see cref="G24SpriteInfo.PageOffsetX"/> and <see cref="G24SpriteInfo.PageOffsetY"/>.
/// Each pixel is a CLUT index; the actual palette is found via the sprite's
/// CLUT id + the tile-cluts count offset in <see cref="G24StyleData.PaletteIndices"/>.
/// </summary>
public static class SpriteRenderer
{
    private const int SpritePageStride = 256;
    private const int SpritePageSize = 256 * 256;
    private const int ClutByteSize = 1024;

    /// <summary>
    /// Decode sprite <paramref name="spriteIndex"/> into a fresh RGBA buffer.
    /// Returns null if the index is out of range or the sprite has zero size.
    /// </summary>
    public static byte[]? DecodeSprite(G24StyleData style, int spriteIndex, out int width, out int height)
    {
        width = 0; height = 0;
        if (spriteIndex < 0 || spriteIndex >= style.Sprites.Count) return null;
        var sprite = style.Sprites[spriteIndex];
        if (sprite.Width == 0 || sprite.Height == 0) return null;
        width = sprite.Width;
        height = sprite.Height;

        // Tile cluts come first in the paged region; the encoded sprite clut id
        // then indexes into the rest of the paletteIndices table.
        int tileClutsCount = (int)(style.Header.TileClutSize / ClutByteSize);
        int paletteSlot = tileClutsCount + sprite.Clut;
        if (paletteSlot < 0 || paletteSlot >= style.PaletteIndices.Length) return null;
        int clutIndex = style.PaletteIndices[paletteSlot];

        var rgba = new byte[width * height * 4];
        var palette = style.PaletteData.AsSpan();
        var gfx = style.SpriteGraphics;
        Span<byte> rgb = stackalloc byte[3];

        for (int y = 0; y < height; y++)
        {
            int srcRow = sprite.PageNumber * SpritePageSize + (sprite.PageOffsetY + y) * SpritePageStride + sprite.PageOffsetX;
            for (int x = 0; x < width; x++)
            {
                int srcIdx = srcRow + x;
                if (srcIdx < 0 || srcIdx >= gfx.Length) continue;
                byte pixel = gfx[srcIdx];
                int dst = (y * width + x) * 4;
                if (pixel == 0) { rgba[dst + 3] = 0; continue; } // pixel 0 is transparent
                if (Palette.LookupColor(palette, clutIndex, pixel, rgb))
                {
                    rgba[dst + 0] = rgb[0];
                    rgba[dst + 1] = rgb[1];
                    rgba[dst + 2] = rgb[2];
                    rgba[dst + 3] = 255;
                }
            }
        }
        return rgba;
    }

    /// <summary>
    /// Translate a car_info record into a global sprite index. Car sprites are
    /// grouped per vehicle class; the spriteNum field is local to that class,
    /// so we sum the sprite-numbers entries for every preceding category.
    /// </summary>
    public static int GetCarSpriteIndex(G24StyleData style, G24CarInfo car)
    {
        var category = (SpriteCategory)CarCategoryFor((VehicleClass)car.VehicleType);
        return CategoryBase(style, category) + car.SpriteNum;
    }

    /// <summary>Cumulative sprite-info offset of <paramref name="category"/>.</summary>
    public static int CategoryBase(G24StyleData style, SpriteCategory category)
    {
        int offset = 0;
        int target = (int)category;
        for (int i = 0; i < target && i < style.SpriteNumbers.Length; i++)
            offset += style.SpriteNumbers[i];
        return offset;
    }

    /// <summary>
    /// Map a CMP vehicle_type byte to the sprite category it draws from.
    /// The category and class enumerations don't share numeric values, so we
    /// translate explicitly.
    /// </summary>
    public static SpriteCategory CarCategoryFor(VehicleClass cls) => cls switch
    {
        VehicleClass.Bus => SpriteCategory.Bus,
        VehicleClass.FrontJuggernaut => SpriteCategory.Bus,
        VehicleClass.BackJuggernaut => SpriteCategory.Bus,
        VehicleClass.Motorcycle => SpriteCategory.Bike,
        VehicleClass.StandardCar => SpriteCategory.Car,
        VehicleClass.Train => SpriteCategory.Train,
        VehicleClass.Tram => SpriteCategory.Tram,
        VehicleClass.Boat => SpriteCategory.Boat,
        VehicleClass.Tank => SpriteCategory.Tank,
        _ => SpriteCategory.Car,
    };
}
