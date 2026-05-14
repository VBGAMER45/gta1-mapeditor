using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using Xunit;

namespace GTA1MapEditor.Tests;

public class CmpReaderTests
{
    [SkippableTheory]
    [InlineData(nameof(TestPaths.NycCmp))]
    [InlineData(nameof(TestPaths.SanbCmp))]
    [InlineData(nameof(TestPaths.MiamiCmp))]
    public void ParsesStockCity(string pathField)
    {
        Skip.IfNot(TestPaths.HasStockData, "Stock GTA1 data not available on this machine.");

        string path = pathField switch
        {
            nameof(TestPaths.NycCmp) => TestPaths.NycCmp,
            nameof(TestPaths.SanbCmp) => TestPaths.SanbCmp,
            nameof(TestPaths.MiamiCmp) => TestPaths.MiamiCmp,
            _ => throw new ArgumentException(null, nameof(pathField)),
        };
        Skip.IfNot(File.Exists(path), $"{path} missing");

        var map = CmpReader.ReadFile(path);

        Assert.NotEqual(0u, map.Header.Version);
        Assert.NotEmpty(map.Blocks);
        Assert.NotEmpty(map.Columns);
        Assert.Equal(GameConfig.MapWidth * GameConfig.MapHeight, map.Base.Length);

        // The map should have at least one walkable road tile.
        bool hasRoad = false;
        for (int y = 0; y < GameConfig.MapHeight && !hasRoad; y++)
        for (int x = 0; x < GameConfig.MapWidth && !hasRoad; x++)
        {
            foreach (var b in map.GetBlockStack(x, y))
                if (b.BlockType == BlockType.Road) { hasRoad = true; break; }
        }
        Assert.True(hasRoad, "Expected at least one ROAD block in stock city.");
    }

    [SkippableFact]
    public void NycHasObjectsAndRoutes()
    {
        Skip.IfNot(File.Exists(TestPaths.NycCmp), "NYC.CMP missing");
        var map = CmpReader.ReadFile(TestPaths.NycCmp);
        Assert.NotEmpty(map.Objects);
        Assert.NotEmpty(map.NavSectors);
    }
}
