using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Shared per-frame sprite pass for objects and cars. Each entity is a flat
/// quad lying on the z = entity.Z plane (+ a small caller-supplied lift to
/// avoid z-fighting with lids), rotated around the vertical axis by
/// <see cref="MapObject.Rotation"/> / <see cref="CarPosition.Rotation"/>.
///
/// Owns its own shader, VAO/VBO, and <see cref="SpriteCache"/>. The same
/// instance is reused across frames; <see cref="SetMap"/> swaps the data.
/// </summary>
public sealed class EntitySpriteRenderer : IDisposable
{
    private const string VertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPos;
        layout(location = 1) in vec2 aUv;
        uniform mat4 uMvp;
        out vec2 vUv;
        void main()
        {
            gl_Position = uMvp * vec4(aPos, 1.0);
            vUv = aUv;
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        in vec2 vUv;
        out vec4 fragColor;
        uniform sampler2D uTex;
        void main()
        {
            vec4 c = texture(uTex, vUv);
            if (c.a < 0.01) discard;
            fragColor = c;
        }
        """;

    private readonly GlShader _shader;
    private int _vao, _vbo;
    private readonly float[] _scratch = new float[30]; // 6 verts * (xyz + uv)
    private CmpMap? _map;
    private G24StyleData? _style;
    private SpriteCache? _cache;
    /// <summary>car_info entries keyed by ModelId — CarPosition.Type is a modelId, not an array index.</summary>
    private readonly Dictionary<byte, G24CarInfo> _carByModelId = new();

    public EntitySpriteRenderer()
    {
        _shader = new GlShader(VertexShader, FragmentShader);
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 20, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 20, 12);
    }

    public void SetMap(CmpMap map, G24StyleData style)
    {
        _map = map;
        _style = style;
        _cache?.Dispose();
        _cache = new SpriteCache(style);
        _carByModelId.Clear();
        foreach (var c in style.Cars) _carByModelId[c.ModelId] = c;
    }

    /// <summary>
    /// Draw every car then every object in <see cref="CmpMap"/>. Cars go first
    /// so objects composite on top when overlapping — matches in-engine z-order.
    /// </summary>
    /// <param name="mvp">View-projection matrix the host renderer is using.</param>
    /// <param name="zLift">World-space lift added to each sprite's z. ~0.02 keeps sprites above lids without visible float; pass 0 for the depth-test-disabled top-down view.</param>
    /// <param name="snapToColumnTop">When true, raise sprites to the top of their column when their stored Z is lower. Necessary for iso/3D where the camera pitch turns any Z mismatch into a screen-space offset — stock-map entities often have Z=0 but render visually on top of the lid stack.</param>
    public void Render(Matrix4 mvp, float zLift, bool snapToColumnTop = false)
    {
        if (_map is null || _style is null || _cache is null) return;

        _shader.Use();
        GL.UniformMatrix4(_shader.GetUniform("uMvp"), false, ref mvp);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.Uniform1(_shader.GetUniform("uTex"), 0);
        GL.BindVertexArray(_vao);

        foreach (var car in _map.CarPositions)
        {
            if (!_carByModelId.TryGetValue(car.Type, out var carInfo)) continue;
            int idx = SpriteRenderer.GetCarSpriteIndex(_style, carInfo);
            DrawSprite(idx, car.X, car.Y, car.Z, car.Rotation, zLift, snapToColumnTop);
        }
        int objectBase = SpriteRenderer.CategoryBase(_style, SpriteCategory.Object);
        foreach (var obj in _map.Objects)
        {
            if (obj.Type >= _style.Objects.Count) continue;
            int idx = objectBase + _style.Objects[obj.Type].BaseSprite;
            DrawSprite(idx, obj.X, obj.Y, obj.Z, obj.Rotation, zLift, snapToColumnTop);
        }
    }

    /// <summary>
    /// Z of the top of the lowest-non-air block in the column. Auto-lift target
    /// for sprites that would otherwise sit below the visible lid stack.
    /// </summary>
    private float GroundTopZ(int tileX, int tileY)
    {
        if (_map is null) return 0;
        var stack = _map.GetBlockStack(tileX, tileY);
        for (int i = 0; i < stack.Count; i++)
        {
            if (stack[i].BlockType == BlockType.Air) continue;
            return i + 1; // top of block at index i in world Z
        }
        return 0;
    }

    private void DrawSprite(int spriteIndex, ushort worldX, ushort worldY, ushort worldZ, ushort rotation, float zLift, bool snapToColumnTop)
    {
        var sprite = _cache!.Get(spriteIndex);
        if (sprite is null) return;

        float ts = GameConfig.TileSize;
        float cx = worldX / ts;
        float cy = worldY / ts;
        float cz = worldZ / ts;
        if (snapToColumnTop)
        {
            float ground = GroundTopZ((int)cx, (int)cy);
            if (ground > cz) cz = ground;
        }
        cz += zLift;
        float halfW = sprite.Value.Width  * 0.5f / ts;
        float halfH = sprite.Value.Height * 0.5f / ts;

        // CMP rotation is a 10-bit fixed-point heading (0..1023 = full turn).
        float angle = rotation * MathF.Tau / 1024f;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        (float x, float y) Rot(float dx, float dy) =>
            (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);

        var (x0, y0) = Rot(-halfW, -halfH);
        var (x1, y1) = Rot( halfW, -halfH);
        var (x2, y2) = Rot( halfW,  halfH);
        var (x3, y3) = Rot(-halfW,  halfH);

        var v = _scratch;
        v[ 0] = x0; v[ 1] = y0; v[ 2] = cz; v[ 3] = 0; v[ 4] = 0;
        v[ 5] = x1; v[ 6] = y1; v[ 7] = cz; v[ 8] = 1; v[ 9] = 0;
        v[10] = x2; v[11] = y2; v[12] = cz; v[13] = 1; v[14] = 1;
        v[15] = x0; v[16] = y0; v[17] = cz; v[18] = 0; v[19] = 0;
        v[20] = x2; v[21] = y2; v[22] = cz; v[23] = 1; v[24] = 1;
        v[25] = x3; v[26] = y3; v[27] = cz; v[28] = 0; v[29] = 1;

        GL.BindTexture(TextureTarget.Texture2D, sprite.Value.Texture);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    public void Dispose()
    {
        _cache?.Dispose();
        _shader.Dispose();
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
    }
}
