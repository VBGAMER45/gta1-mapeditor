namespace GTA1MapEditor.Tests;

/// <summary>
/// Paths to stock GTA1 game data borrowed from the sibling web project.
/// Tests that depend on these files are skipped when the path doesn't exist
/// so the suite stays green on machines without the assets.
/// </summary>
internal static class TestPaths
{
    private static readonly string GtaAssets =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..", "gta", "public", "assets"));

    public static string NycCmp    => Path.Combine(GtaAssets, "maps", "NYC.CMP");
    public static string SanbCmp   => Path.Combine(GtaAssets, "maps", "SANB.CMP");
    public static string MiamiCmp  => Path.Combine(GtaAssets, "maps", "MIAMI.CMP");
    public static string Style001  => Path.Combine(GtaAssets, "styles", "style001", "style001.g24");
    public static string Style002  => Path.Combine(GtaAssets, "styles", "style002", "style002.g24");
    public static string Style003  => Path.Combine(GtaAssets, "styles", "style003", "style003.g24");

    public static bool HasStockData => File.Exists(NycCmp) && File.Exists(Style001);
}
