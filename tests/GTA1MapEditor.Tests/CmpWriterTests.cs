using GTA1MapEditor.Core;
using Xunit;

namespace GTA1MapEditor.Tests;

public class CmpWriterTests
{
    [SkippableFact]
    public void RoundTripsNyc()
    {
        Skip.IfNot(File.Exists(TestPaths.NycCmp), "NYC.CMP missing");
        var original = CmpReader.ReadFile(TestPaths.NycCmp);
        var bytes = CmpWriter.Write(original);
        var roundTripped = CmpReader.Read(bytes);

        Assert.Equal(original.Header.Version, roundTripped.Header.Version);
        Assert.Equal(original.Header.StyleNumber, roundTripped.Header.StyleNumber);
        Assert.Equal(original.Blocks.Count, roundTripped.Blocks.Count);
        Assert.Equal(original.Columns.Length, roundTripped.Columns.Length);
        Assert.Equal(original.Objects.Count, roundTripped.Objects.Count);
        Assert.Equal(original.CarPositions.Count, roundTripped.CarPositions.Count);
        Assert.Equal(original.Routes.Count, roundTripped.Routes.Count);
        Assert.Equal(original.NavSectors.Count, roundTripped.NavSectors.Count);
        Assert.Equal(original.SpawnLocations.Count, roundTripped.SpawnLocations.Count);

        // Spot-check a tile deep in Manhattan: same block stack after round trip.
        var origStack = original.GetBlockStack(128, 128);
        var newStack = roundTripped.GetBlockStack(128, 128);
        Assert.Equal(origStack.Count, newStack.Count);
        for (int i = 0; i < origStack.Count; i++)
        {
            Assert.Equal(origStack[i].TypeMap, newStack[i].TypeMap);
            Assert.Equal(origStack[i].TypeMapExt, newStack[i].TypeMapExt);
            Assert.Equal(origStack[i].Lid, newStack[i].Lid);
        }
    }
}
