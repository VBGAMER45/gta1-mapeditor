using GTA1MapEditor.Core;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Builds a Z-axis rotation around the map center for the global "rotate map"
/// feature. Same yaw value across all three views so users can compare
/// 2D / iso / 3D in the same orientation.
/// </summary>
public static class WorldRotation
{
    /// <summary>Yaw is 0..3 quarter-turns clockwise. Returns identity for 0.</summary>
    public static Matrix4 Build(int yaw)
    {
        yaw &= 3;
        if (yaw == 0) return Matrix4.Identity;
        float theta = yaw * MathF.PI / 2f;
        float cx = GameConfig.MapWidth / 2f;
        float cy = GameConfig.MapHeight / 2f;
        // OpenTK row-vector convention: v * (T1 * R * T2) applies T1 first.
        return Matrix4.CreateTranslation(-cx, -cy, 0)
             * Matrix4.CreateRotationZ(theta)
             * Matrix4.CreateTranslation(cx, cy, 0);
    }

    /// <summary>Apply the inverse of <see cref="Build(int)"/> to a 2D world point. Used by PickTile to undo the rendered rotation when going from screen to tile.</summary>
    public static Vector2 InverseRotate(int yaw, Vector2 p)
    {
        yaw &= 3;
        if (yaw == 0) return p;
        float theta = -yaw * MathF.PI / 2f;
        float cx = GameConfig.MapWidth / 2f;
        float cy = GameConfig.MapHeight / 2f;
        float dx = p.X - cx, dy = p.Y - cy;
        float cos = MathF.Cos(theta), sin = MathF.Sin(theta);
        return new Vector2(cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);
    }
}
