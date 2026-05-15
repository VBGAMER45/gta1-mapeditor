using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;
using Xunit.Abstractions;

namespace GTA1MapEditor.Tests;

/// <summary>
/// Byte-level diagnostic: decode a few tiles from style001.g24 using the SAME
/// pipeline as TileAtlas.Build and compare against the reference pre-extracted
/// PNGs in the sibling web project. The first mismatch tells us whether the
/// decode (pixel indexing or palette formula) is wrong, or — if everything
/// matches — that the bug lies in the renderer's atlas-slot selection.
/// </summary>
public class TileDecodeVsPngTests
{
    private const int TileSize = 64;

    private readonly ITestOutputHelper _out;

    public TileDecodeVsPngTests(ITestOutputHelper output) => _out = output;

    private static string PngRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "gta",
            "public", "assets", "styles", "style001", "tiles"));

    public static IEnumerable<object[]> Cases() => new[]
    {
        // section, indexWithinSection
        new object[] { "side", 0 },
        new object[] { "lid",  0 },
        new object[] { "lid",  42 },
        new object[] { "aux",  0 },
    };

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public void DecodedTileMatchesReferencePng(string section, int indexWithinSection)
    {
        Skip.IfNot(File.Exists(TestPaths.Style001), $"{TestPaths.Style001} missing");

        string pngFolder = section switch
        {
            "side" => Path.Combine(PngRoot, "side"),
            "lid"  => Path.Combine(PngRoot, "lid"),
            "aux"  => Path.Combine(PngRoot, "auxiliary"),
            _ => throw new ArgumentException(null, nameof(section)),
        };
        string prefix = section switch { "aux" => "aux", _ => section };
        string pngPath = Path.Combine(pngFolder, $"{prefix}_{indexWithinSection:D4}.png");
        Skip.IfNot(File.Exists(pngPath), $"{pngPath} missing");

        var style = G24Reader.ReadFile(TestPaths.Style001);

        // Reference PNG files appear to use a +1 atlas index (i.e. the web
        // project's extractor skipped atlas slot 0 as a reserved blank, so
        // side_0000.png == atlas slot 1, lid_0000.png == sideCount + 1, etc.).
        // The empirical probe (deleted) confirmed this by exhaustively
        // searching tile×CLUT space for each PNG.
        int atlasIndex = section switch
        {
            "side" => indexWithinSection + 1,
            "lid"  => style.SideTileCount + indexWithinSection + 1,
            "aux"  => style.SideTileCount + style.LidTileCount + indexWithinSection + 1,
            _ => throw new ArgumentException(null, nameof(section)),
        };

        // Capture the raw pixel indices for the first row before decoding,
        // for the diagnostic dump on mismatch. Mirrors TileAtlas.Build addressing.
        const int RowStride = 4 * TileSize;
        const int BlockRowBytes = TileSize * RowStride;
        int blockX = atlasIndex % 4;
        int blockY = atlasIndex / 4;
        int tileBase = blockY * BlockRowBytes + blockX * TileSize;
        byte[] firstRowIndices = new byte[16];
        for (int i = 0; i < 16; i++) firstRowIndices[i] = style.TileData[tileBase + i];

        int paletteSlot = 4 * atlasIndex;
        int clutIndex = paletteSlot < style.PaletteIndices.Length
            ? style.PaletteIndices[paletteSlot] : 0;

        byte[] decoded = DecodeTileExactlyLikeAtlas(style, atlasIndex);

        // Reference PNG → RGBA.
        byte[] reference = LoadPngAsRgba(pngPath, out int pw, out int ph);
        Assert.Equal(TileSize, pw);
        Assert.Equal(TileSize, ph);

        // Compare RGB only (the atlas writes 0 for index-0 pixels and
        // 255 elsewhere — the PNG extractor sometimes leaves transparent
        // pixels as opaque-black instead, which is harmless).
        int firstMismatch = -1;
        int diffCount = 0;
        for (int i = 0; i < TileSize * TileSize; i++)
        {
            int o = i * 4;
            bool decTransparent = decoded[o + 3] == 0;
            bool refTransparent = reference[o + 3] == 0;
            // Treat both-transparent as equal regardless of RGB.
            if (decTransparent && refTransparent) continue;
            // Decoded-transparent vs PNG opaque-black: also harmless (alpha drift).
            if (decTransparent && reference[o] == 0 && reference[o + 1] == 0 && reference[o + 2] == 0) continue;
            bool rgbEqual = decoded[o] == reference[o]
                         && decoded[o + 1] == reference[o + 1]
                         && decoded[o + 2] == reference[o + 2];
            if (!rgbEqual)
            {
                if (firstMismatch < 0) firstMismatch = i;
                diffCount++;
            }
        }

        if (diffCount != 0)
        {
            _out.WriteLine($"[{section} #{indexWithinSection}] atlasIndex={atlasIndex} clutIndex={clutIndex} paletteIndices[{paletteSlot}]={(paletteSlot < style.PaletteIndices.Length ? style.PaletteIndices[paletteSlot].ToString() : "OOR")}");
            _out.WriteLine($"   tileBase=0x{tileBase:X} blockX={blockX} blockY={blockY}");
            _out.WriteLine($"   diffCount={diffCount}/{TileSize * TileSize}  firstMismatch=pixel#{firstMismatch}");

            _out.WriteLine("   first 16 G24 pixel INDICES (row 0):");
            _out.WriteLine("     " + string.Join(" ", firstRowIndices.Select(b => b.ToString("X2"))));

            _out.WriteLine("   first 16 decoded RGBA (row 0):");
            _out.WriteLine("     " + DumpRgba(decoded, 0, 16));

            _out.WriteLine("   first 16 reference PNG RGBA (row 0):");
            _out.WriteLine("     " + DumpRgba(reference, 0, 16));

            // Search whether ANY CLUT in this style would map the captured
            // index bytes to the reference colours — points at palette
            // selection vs. palette decoding.
            int clutCount = style.PaletteData.Length / 4 / 256; // total CLUT slots
            int matchingClut = FindMatchingClut(style, firstRowIndices, reference, 16);
            if (matchingClut >= 0)
                _out.WriteLine($"   NOTE: reference colours match CLUT #{matchingClut} (current pipeline used CLUT #{clutIndex}). Likely a paletteIndex selection bug.");
            else
                _out.WriteLine($"   NOTE: no CLUT in this style ({clutCount} total) maps the captured indices to the reference colours. Likely a pixel-addressing bug (wrong source bytes).");
        }

        Assert.Equal(0, diffCount);
    }

    // EXACT copy of the decode in TileAtlas.Build's inner lambda.
    private static byte[] DecodeTileExactlyLikeAtlas(G24StyleData style, int tile)
    {
        var palette = style.PaletteData;
        int paletteSlot = 4 * tile;
        int clutIndex = paletteSlot < style.PaletteIndices.Length
            ? style.PaletteIndices[paletteSlot] : 0;

        const int RowStride = 4 * TileSize;
        const int BlockRowBytes = TileSize * RowStride;
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
    }

    private static byte[] LoadPngAsRgba(string path, out int width, out int height)
    {
        using var img = Image.Load<Rgba32>(path);
        width = img.Width;
        height = img.Height;
        var buf = new byte[width * height * 4];
        img.CopyPixelDataTo(buf);
        return buf;
    }

    private static string DumpRgba(byte[] rgba, int startPixel, int count)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
        {
            int o = (startPixel + i) * 4;
            sb.Append($"{rgba[o]:X2}{rgba[o + 1]:X2}{rgba[o + 2]:X2}{rgba[o + 3]:X2} ");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Search every CLUT for one whose colours match the reference PNG given
    /// the captured indices. -1 if no perfect match within the first
    /// <paramref name="probePixels"/> pixels.
    /// </summary>
    private static int FindMatchingClut(G24StyleData style, byte[] indices, byte[] referenceRgba, int probePixels)
    {
        var paletteSpan = style.PaletteData.AsSpan();
        int totalCluts = style.PaletteData.Length / Palette.ClutPageSize * Palette.ClutsPerPage;
        Span<byte> rgb = stackalloc byte[3];
        for (int ci = 0; ci < totalCluts; ci++)
        {
            bool ok = true;
            for (int i = 0; i < probePixels; i++)
            {
                byte ix = indices[i];
                int o = i * 4;
                // Skip transparent reference pixels (index 0) since RGB undefined.
                if (referenceRgba[o + 3] == 0 && ix == 0) continue;
                if (!Palette.LookupColor(paletteSpan, ci, ix, rgb)) { ok = false; break; }
                if (rgb[0] != referenceRgba[o] || rgb[1] != referenceRgba[o + 1] || rgb[2] != referenceRgba[o + 2])
                { ok = false; break; }
            }
            if (ok) return ci;
        }
        return -1;
    }
}
