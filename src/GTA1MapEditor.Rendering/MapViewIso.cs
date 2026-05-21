using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// 2.5D dimetric isometric view. Builds the full mesh (lids + walls) and
/// renders with an orthographic camera tilted 30° down and yawed 45° around
/// the vertical axis. Depth buffer handles occlusion so we don't have to
/// painter-sort.
/// </summary>
public sealed class MapViewIso : IMapView
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
    private readonly EntitySpriteRenderer _entitySprites = new();
    private int _vao, _vbo;
    private int _atlasTex;
    private int _vertexCount;
    private TileAtlas? _atlas;
    private CmpMap? _map;
    private G24StyleData? _style;

    // Camera state
    public Vector3 Target { get; set; } = new(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f, 0f);

    public Vector2 CameraWorld
    {
        get => new(Target.X, Target.Y);
        set => Target = new Vector3(value.X, value.Y, Target.Z);
    }

    /// <summary>Half-height of the orthographic frustum in world units. Smaller = more zoomed-in.</summary>
    public float OrthoSize { get; set; } = 64f;

    public const float MinOrthoSize = 4f;
    public const float MaxOrthoSize = 256f;

    public float ViewportWidth { get; private set; } = 1;
    public float ViewportHeight { get; private set; } = 1;

    public (int x, int y, int z)? Selection { get; set; }
    public int MapYaw { get; set; }
    public bool UsesFlyCamera => false;

    public MapViewIso()
    {
        _shader = new GlShader(VertexShader, FragmentShader);
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, MapMesh.Stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, MapMesh.Stride, 12);
        _atlasTex = GL.GenTexture();
    }

    public void SetMap(CmpMap map, G24StyleData style)
    {
        _map = map;
        _style = style;
        _atlas = TileAtlas.Build(style);
        _entitySprites.SetMap(map, style);
        UploadAtlas();
        RebuildMesh();
    }

    public void RebuildMesh()
    {
        if (_map is null || _atlas is null || _style is null) { _vertexCount = 0; return; }
        var verts = MapMesh.BuildFull(_map, _atlas, _style.SideTileCount);
        _vertexCount = verts.Length / MapMesh.FloatsPerVertex;
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Length * sizeof(float),
            verts, BufferUsageHint.StaticDraw);
    }

    public void Render()
    {
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Less);
        GL.ClearColor(0.08f, 0.08f, 0.12f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var mvp = BuildMvp();
        if (_vertexCount > 0)
        {
            _shader.Use();
            GL.UniformMatrix4(_shader.GetUniform("uMvp"), false, ref mvp);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _atlasTex);
            GL.Uniform1(_shader.GetUniform("uTex"), 0);
            GL.BindVertexArray(_vao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
        }

        // Sprites: depth-test against walls/lids (so a sprite behind a building
        // is properly occluded) but don't WRITE depth — that lets us draw them
        // at the exact entity Z without z-fighting the lid below, and avoids
        // any visible vertical shift in the iso projection. Lequal so a sprite
        // sitting flush on a lid (same z) still draws.
        GL.DepthFunc(DepthFunction.Lequal);
        GL.DepthMask(false);
        _entitySprites.Render(mvp, zLift: 0f, snapToColumnTop: true);
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
    }

    public void Resize(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
        GL.Viewport(0, 0, width, height);
    }

    public void Pan(float screenDx, float screenDy)
    {
        // Pan is interpreted in screen-aligned axes mapped back to the iso plane.
        // For dimetric we project the screen vector onto the world xy plane.
        float pixelsPerUnit = ViewportHeight / (2f * OrthoSize);
        float wx = screenDx / pixelsPerUnit;
        float wy = screenDy / pixelsPerUnit;
        // Inverse of the 2:1 dimetric projection: (sx, sy) → world (wx, wy).
        //   sx = (x - y) * cos45;   sy = (x + y) * sin45 * 0.866; (after rotateZ45 + tiltX30)
        // Use approximate inverse for snappy pan rather than a full unproject.
        float dx = wx + wy;
        float dy = -wx + wy;
        Target -= new Vector3(dx, dy, 0);
    }

    public void Zoom(float factor, int anchorX, int anchorY)
    {
        OrthoSize = Math.Clamp(OrthoSize / factor, MinOrthoSize, MaxOrthoSize);
    }

    public void ResetView()
    {
        Target = new Vector3(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f, 0f);
        OrthoSize = 64f;
    }

    public void FitMap()
    {
        Target = new Vector3(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f, 0f);
        OrthoSize = GameConfig.MapWidth * 0.6f;
    }

    public void NativeZoom()
    {
        // Pick an OrthoSize that makes one tile ≈ 64 screen pixels tall after
        // the 2:1 dimetric squash. OrthoSize is half-height in world units, so
        // world_units_visible = 2 * OrthoSize; pixels_per_unit = viewportH / (2*OrthoSize).
        // Want pixels_per_tile_visual ≈ 64, where the squash projects 1 tile of Y
        // into ~0.5 world units → OrthoSize = viewportH / (2 * 64 * 0.5) = viewportH / 64.
        OrthoSize = Math.Clamp(ViewportHeight / 64f, MinOrthoSize, MaxOrthoSize);
    }

    public (int x, int y)? PickTile(int screenX, int screenY)
    {
        // Inverse-project to the z=0 plane of the iso view.
        var mvp = BuildMvp();
        Matrix4 inv;
        try { inv = Matrix4.Invert(mvp); }
        catch { return null; }
        // NDC ray from near to far plane.
        float ndcX = (screenX / ViewportWidth) * 2f - 1f;
        float ndcY = 1f - (screenY / ViewportHeight) * 2f;
        var nearW = Vector4.TransformRow(new Vector4(ndcX, ndcY, -1, 1), inv);
        var farW  = Vector4.TransformRow(new Vector4(ndcX, ndcY,  1, 1), inv);
        var n = nearW.Xyz / nearW.W;
        var f = farW.Xyz / farW.W;
        // Intersect with z=0.
        if (Math.Abs(f.Z - n.Z) < 1e-5f) return null;
        float t = -n.Z / (f.Z - n.Z);
        var hit = n + (f - n) * t;
        int tx = (int)Math.Floor(hit.X);
        int ty = (int)Math.Floor(hit.Y);
        if (tx < 0 || tx >= GameConfig.MapWidth || ty < 0 || ty >= GameConfig.MapHeight) return null;
        return (tx, ty);
    }

    public void ApplyLookDelta(float dx, float dy) { /* iso has fixed camera angle */ }
    public void Tick(float dt, bool fwd, bool back, bool left, bool right, bool up, bool down) { }

    public void Dispose()
    {
        _entitySprites.Dispose();
        _shader.Dispose();
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteTexture(_atlasTex);
    }

    private void UploadAtlas()
    {
        if (_atlas is null) return;
        AtlasTexture.Upload(_atlasTex, _atlas);
    }

    private Matrix4 BuildMvp()
    {
        // Camera sits SE-above the target, looks at it. The "south-east" of a
        // GTA1 map (where Y grows southward) is +X +Y, so the eye lives there.
        const float yawDeg = 45f;       // azimuth: 45° between +X and +Y
        const float pitchDeg = 30f;     // elevation above horizon
        float yaw = MathHelper.DegreesToRadians(yawDeg);
        float pitch = MathHelper.DegreesToRadians(pitchDeg);
        float dist = OrthoSize * 4f;    // far enough that ortho frustum captures everything
        var eye = Target + new Vector3(
            MathF.Cos(yaw) * MathF.Cos(pitch) * dist,
            MathF.Sin(yaw) * MathF.Cos(pitch) * dist,
            MathF.Sin(pitch) * dist);
        var view = Matrix4.LookAt(eye, Target, Vector3.UnitZ);

        float aspect = ViewportWidth / ViewportHeight;
        var proj = Matrix4.CreateOrthographic(OrthoSize * aspect, OrthoSize, -1024, 1024);
        // World rotation goes first (applied to vertices before view*proj)
        // so the shared MapYaw aligns the iso view with the other modes.
        return WorldRotation.Build(MapYaw) * view * proj;
    }
}
