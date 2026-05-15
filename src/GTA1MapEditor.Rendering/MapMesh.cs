using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Generates triangle-mesh vertex data for the map. Vertices are
/// position.xyz + uv (5 floats each, 20-byte stride). Y grows southward and
/// Z grows upward, matching the CMP convention.
///
/// Three flavours:
///   Full     — every visible lid + every textured wall in every column
///              (used by iso and 3D)
///   TopOnly  — just the topmost lid per tile (used by 2D top-down)
/// </summary>
public static class MapMesh
{
    /// <summary>Bytes per vertex (5 floats).</summary>
    public const int Stride = 20;

    /// <summary>Floats per vertex.</summary>
    public const int FloatsPerVertex = 5;

    /// <summary>
    /// Lids + four walls for every block in every column. Block bytes are
    /// 1-based section-relative: lid N → atlas (sideTileCount + N - 1),
    /// wall N → atlas (N - 1). 0 = no texture (skipped).
    /// </summary>
    public static float[] BuildFull(CmpMap map, TileAtlas atlas, int sideTileCount)
    {
        var verts = new List<float>(1 << 16);
        for (int ty = 0; ty < GameConfig.MapHeight; ty++)
        for (int tx = 0; tx < GameConfig.MapWidth; tx++)
        {
            var stack = map.GetBlockStack(tx, ty);
            for (int z = 0; z < stack.Count; z++)
            {
                var b = stack[z];

                int lidAtlas = b.Lid    > 0 ? sideTileCount + b.Lid - 1 : -1;
                int topAtlas = b.Top    > 0 ? b.Top    - 1 : -1;
                int botAtlas = b.Bottom > 0 ? b.Bottom - 1 : -1;
                int lftAtlas = b.Left   > 0 ? b.Left   - 1 : -1;
                int rgtAtlas = b.Right  > 0 ? b.Right  - 1 : -1;

                if (lidAtlas >= 0 && lidAtlas < atlas.TileCount)
                    EmitLid(verts, atlas, tx, ty, z, b, lidAtlas);
                if (topAtlas >= 0 && topAtlas < atlas.TileCount)
                    EmitNorthWall(verts, atlas, tx, ty, z, topAtlas, b.FlipLeftRight);
                if (botAtlas >= 0 && botAtlas < atlas.TileCount)
                    EmitSouthWall(verts, atlas, tx, ty, z, botAtlas, b.FlipLeftRight);
                if (lftAtlas >= 0 && lftAtlas < atlas.TileCount)
                    EmitWestWall(verts, atlas, tx, ty, z, lftAtlas, b.FlipLeftRight);
                if (rgtAtlas >= 0 && rgtAtlas < atlas.TileCount)
                    EmitEastWall(verts, atlas, tx, ty, z, rgtAtlas, b.FlipLeftRight);
            }
        }
        return verts.ToArray();
    }

    public static float[] BuildTopOnly(CmpMap map, TileAtlas atlas, int sideTileCount)
    {
        var verts = new List<float>(GameConfig.MapWidth * GameConfig.MapHeight * 6 * FloatsPerVertex);
        for (int ty = 0; ty < GameConfig.MapHeight; ty++)
        for (int tx = 0; tx < GameConfig.MapWidth; tx++)
        {
            var stack = map.GetBlockStack(tx, ty);
            BlockInfo? top = null;
            for (int i = stack.Count - 1; i >= 0; i--)
                if (stack[i].Lid != 0) { top = stack[i]; break; }
            if (top is null) continue;
            int atlasIdx = sideTileCount + top.Lid - 1;
            if (atlasIdx < 0 || atlasIdx >= atlas.TileCount) continue;
            EmitLidFlat(verts, atlas, tx, ty, 0, top, atlasIdx);
        }
        return verts.ToArray();
    }

    // ─── Quad emission ────────────────────────────────────────────────────

    private static void EmitLid(List<float> verts, TileAtlas atlas, int tx, int ty, int z, BlockInfo b, int atlasTile)
    {
        var (u0, v0, u1, v1) = atlas.GetUv(atlasTile);
        var nw = new Vector2(u0, v0);
        var ne = new Vector2(u1, v0);
        var se = new Vector2(u1, v1);
        var sw = new Vector2(u0, v1);
        // FLIP_LR is side-face-only per Carnage3D — applying it to lids
        // mirrors corner trim incorrectly. Rotation alone drives lid
        // orientation. (Web port fix: commit 2893914.)
        for (int r = 0; r < (int)b.Rotation; r++)
            (nw, ne, se, sw) = (sw, nw, ne, se);

        // Corner heights from slope table (defaults to a flat top of cube).
        var corners = SlopeGeometry.GetCorners(b.SlopeType) ?? new SlopeCorners(1f, 1f, 1f, 1f);
        float zNw = z + corners.Nw;
        float zNe = z + corners.Ne;
        float zSe = z + corners.Se;
        float zSw = z + corners.Sw;

        EmitTri(verts, tx,     ty,     zNw, nw,
                       tx + 1, ty,     zNe, ne,
                       tx + 1, ty + 1, zSe, se);
        EmitTri(verts, tx,     ty,     zNw, nw,
                       tx + 1, ty + 1, zSe, se,
                       tx,     ty + 1, zSw, sw);
    }

    /// <summary>Lid emitter that ignores slope (used for top-down view at a constant z).</summary>
    private static void EmitLidFlat(List<float> verts, TileAtlas atlas, int tx, int ty, float z, BlockInfo b, int atlasTile)
    {
        var (u0, v0, u1, v1) = atlas.GetUv(atlasTile);
        var nw = new Vector2(u0, v0);
        var ne = new Vector2(u1, v0);
        var se = new Vector2(u1, v1);
        var sw = new Vector2(u0, v1);
        // FLIP_LR is side-face-only per Carnage3D — see comment in EmitLid.
        for (int r = 0; r < (int)b.Rotation; r++)
            (nw, ne, se, sw) = (sw, nw, ne, se);

        EmitTri(verts, tx,     ty,     z, nw,
                       tx + 1, ty,     z, ne,
                       tx + 1, ty + 1, z, se);
        EmitTri(verts, tx,     ty,     z, nw,
                       tx + 1, ty + 1, z, se,
                       tx,     ty + 1, z, sw);
    }

    private static void EmitNorthWall(List<float> verts, TileAtlas atlas, int tx, int ty, int z, int tile, bool flip)
    {
        var (u0, v0, u1, v1) = atlas.GetUv(tile);
        if (flip) (u0, u1) = (u1, u0);
        // North wall sits at y = ty (the -Y side). Y is constant.
        EmitTri(verts, tx,     ty, z + 1, new Vector2(u0, v0),
                       tx + 1, ty, z + 1, new Vector2(u1, v0),
                       tx + 1, ty, z,     new Vector2(u1, v1));
        EmitTri(verts, tx,     ty, z + 1, new Vector2(u0, v0),
                       tx + 1, ty, z,     new Vector2(u1, v1),
                       tx,     ty, z,     new Vector2(u0, v1));
    }

    private static void EmitSouthWall(List<float> verts, TileAtlas atlas, int tx, int ty, int z, int tile, bool flip)
    {
        var (u0, v0, u1, v1) = atlas.GetUv(tile);
        if (flip) (u0, u1) = (u1, u0);
        // South wall at y = ty + 1.
        EmitTri(verts, tx + 1, ty + 1, z + 1, new Vector2(u0, v0),
                       tx,     ty + 1, z + 1, new Vector2(u1, v0),
                       tx,     ty + 1, z,     new Vector2(u1, v1));
        EmitTri(verts, tx + 1, ty + 1, z + 1, new Vector2(u0, v0),
                       tx,     ty + 1, z,     new Vector2(u1, v1),
                       tx + 1, ty + 1, z,     new Vector2(u0, v1));
    }

    private static void EmitWestWall(List<float> verts, TileAtlas atlas, int tx, int ty, int z, int tile, bool flip)
    {
        var (u0, v0, u1, v1) = atlas.GetUv(tile);
        if (flip) (u0, u1) = (u1, u0);
        // West wall at x = tx.
        EmitTri(verts, tx, ty,     z + 1, new Vector2(u0, v0),
                       tx, ty + 1, z + 1, new Vector2(u1, v0),
                       tx, ty + 1, z,     new Vector2(u1, v1));
        EmitTri(verts, tx, ty,     z + 1, new Vector2(u0, v0),
                       tx, ty + 1, z,     new Vector2(u1, v1),
                       tx, ty,     z,     new Vector2(u0, v1));
    }

    private static void EmitEastWall(List<float> verts, TileAtlas atlas, int tx, int ty, int z, int tile, bool flip)
    {
        var (u0, v0, u1, v1) = atlas.GetUv(tile);
        if (flip) (u0, u1) = (u1, u0);
        // East wall at x = tx + 1.
        EmitTri(verts, tx + 1, ty + 1, z + 1, new Vector2(u0, v0),
                       tx + 1, ty,     z + 1, new Vector2(u1, v0),
                       tx + 1, ty,     z,     new Vector2(u1, v1));
        EmitTri(verts, tx + 1, ty + 1, z + 1, new Vector2(u0, v0),
                       tx + 1, ty,     z,     new Vector2(u1, v1),
                       tx + 1, ty + 1, z,     new Vector2(u0, v1));
    }

    private static void EmitTri(
        List<float> v,
        float x1, float y1, float z1, Vector2 uv1,
        float x2, float y2, float z2, Vector2 uv2,
        float x3, float y3, float z3, Vector2 uv3)
    {
        v.Add(x1); v.Add(y1); v.Add(z1); v.Add(uv1.X); v.Add(uv1.Y);
        v.Add(x2); v.Add(y2); v.Add(z2); v.Add(uv2.X); v.Add(uv2.Y);
        v.Add(x3); v.Add(y3); v.Add(z3); v.Add(uv3.X); v.Add(uv3.Y);
    }
}
