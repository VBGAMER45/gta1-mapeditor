using GTA1MapEditor.Core;
using Xunit;

namespace GTA1MapEditor.Tests;

public class G24ReaderTests
{
    [SkippableTheory]
    [InlineData(nameof(TestPaths.Style001))]
    [InlineData(nameof(TestPaths.Style002))]
    [InlineData(nameof(TestPaths.Style003))]
    public void ParsesStockStyle(string field)
    {
        string path = field switch
        {
            nameof(TestPaths.Style001) => TestPaths.Style001,
            nameof(TestPaths.Style002) => TestPaths.Style002,
            nameof(TestPaths.Style003) => TestPaths.Style003,
            _ => throw new ArgumentException(null, nameof(field)),
        };
        Skip.IfNot(File.Exists(path), $"{path} missing");

        var style = G24Reader.ReadFile(path);

        Assert.NotEqual(0u, style.Header.VersionCode);
        Assert.NotEmpty(style.Cars);
        Assert.NotEmpty(style.Sprites);
        Assert.NotEmpty(style.Objects);
        Assert.True(style.PaletteData.Length >= Palette.ClutPageSize);
        Assert.True(style.TileData.Length > 0);

        // SpriteNumbers should expose all 21 categories
        Assert.True(style.SpriteNumbers.Length >= 21);

        // Palette lookup should return a real colour for CLUT 0, pixel 1.
        Span<byte> rgb = stackalloc byte[3];
        Assert.True(Palette.LookupColor(style.PaletteData, 0, 1, rgb));
    }
}
