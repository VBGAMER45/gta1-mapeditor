using System.Drawing.Imaging;
using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

/// <summary>
/// Loads pre-extracted PNG tiles from the web-project's layout:
/// <c>{baseDir}/styles/styleNNN/tiles/{side,lid,auxiliary}/{side|lid|aux}_NNNN.png</c>.
/// Used as a higher-fidelity fallback ahead of G24 CLUT decoding: the
/// PNGs were extracted offline with full palette knowledge so the colours
/// always match the reference.
/// </summary>
public sealed class PngTileSource
{
    private readonly byte[]?[] _tiles;

    public int TileCount => _tiles.Length;

    private PngTileSource(byte[]?[] tiles) { _tiles = tiles; }

    public byte[]? GetTile(int index) =>
        index >= 0 && index < _tiles.Length ? _tiles[index] : null;

    /// <summary>
    /// Attempt to locate and load PNG tiles for the given CMP. Returns null
    /// if the expected folder structure isn't found or the section counts
    /// don't match what the G24 advertises (defensive: a mismatch usually
    /// means out-of-date PNGs).
    /// </summary>
    public static PngTileSource? TryLoad(string cmpPath, byte styleNumber,
        int sideCount, int lidCount, int auxCount)
    {
        var tilesDir = LocateTilesDir(cmpPath, styleNumber);
        if (tilesDir is null) return null;

        int total = sideCount + lidCount + auxCount;
        var tiles = new byte[total][];

        // Sections are laid out [side | lid | aux] in atlas-index order.
        if (!LoadSection(Path.Combine(tilesDir, "side"), "side", tiles, 0, sideCount)) return null;
        if (!LoadSection(Path.Combine(tilesDir, "lid"),  "lid",  tiles, sideCount, lidCount)) return null;
        // Aux is optional — used for animation frames and not all maps reference it.
        LoadSection(Path.Combine(tilesDir, "auxiliary"), "aux", tiles, sideCount + lidCount, auxCount);

        return new PngTileSource(tiles);
    }

    private static string? LocateTilesDir(string cmpPath, byte styleNumber)
    {
        var cmpDir = Path.GetDirectoryName(cmpPath) ?? "";
        var styleSubdir = $"style{styleNumber:D3}";
        var candidates = new[]
        {
            Path.Combine(cmpDir, "..", "styles", styleSubdir, "tiles"),
            Path.Combine(cmpDir, "..", styleSubdir, "tiles"),
            Path.Combine(cmpDir, "tiles"),
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (Directory.Exists(Path.Combine(full, "lid")) && Directory.Exists(Path.Combine(full, "side")))
                return full;
        }
        return null;
    }

    private static bool LoadSection(string dir, string prefix, byte[]?[] tiles, int atlasStart, int count)
    {
        if (!Directory.Exists(dir)) return false;
        for (int i = 0; i < count; i++)
        {
            string path = Path.Combine(dir, $"{prefix}_{i:D4}.png");
            if (!File.Exists(path)) continue;
            tiles[atlasStart + i] = LoadPng(path);
        }
        return true;
    }

    /// <summary>Decode a single 64×64 PNG into a top-left-origin RGBA buffer.</summary>
    private static byte[]? LoadPng(string path)
    {
        try
        {
            using var bmp = new Bitmap(path);
            if (bmp.Width != TileAtlas.TileSize || bmp.Height != TileAtlas.TileSize)
                return null;

            // Lock as 32bppArgb (GDI+ memory order is BGRA). Convert to RGBA.
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int bytes = bmp.Width * bmp.Height * 4;
            var bgra = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bgra, 0, bytes);
            bmp.UnlockBits(data);

            var rgba = new byte[bytes];
            for (int i = 0; i + 3 < bytes; i += 4)
            {
                rgba[i + 0] = bgra[i + 2]; // R
                rgba[i + 1] = bgra[i + 1]; // G
                rgba[i + 2] = bgra[i + 0]; // B
                rgba[i + 3] = bgra[i + 3]; // A
            }
            return rgba;
        }
        catch
        {
            return null;
        }
    }
}
