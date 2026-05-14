namespace GTA1MapEditor.Core;

/// <summary>
/// Lookup helpers for the paged 24-bit CLUT region of a G24 style file.
/// 64 CLUTs share each 64 KiB page; pixels within a CLUT are stored at
/// stride 256 with BGR + pad ordering.
/// </summary>
public static class Palette
{
    public const int ClutPageSize = 64 * 1024;
    public const int ClutsPerPage = 64;
    public const int ClutEntryStride = 4;

    /// <summary>
    /// Decodes pixel <paramref name="pixel"/> using CLUT <paramref name="clutIndex"/>.
    /// Writes (R, G, B) to <paramref name="rgb"/>. Returns false if the lookup is out of range.
    /// </summary>
    public static bool LookupColor(ReadOnlySpan<byte> paletteData, int clutIndex, int pixel, Span<byte> rgb)
    {
        int page = clutIndex / ClutsPerPage;
        int slot = clutIndex % ClutsPerPage;
        int offset = page * ClutPageSize + slot * ClutEntryStride + pixel * 256;
        if (offset + 2 >= paletteData.Length) return false;
        rgb[0] = paletteData[offset + 2]; // R  (paged CLUT stores BGR + pad)
        rgb[1] = paletteData[offset + 1]; // G
        rgb[2] = paletteData[offset + 0]; // B
        return true;
    }
}
