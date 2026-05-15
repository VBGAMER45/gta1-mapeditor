using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Perspective free-look renderer. WASD moves the eye, drag-look turns it,
/// Q/E ascend/descend. Mesh is identical to the iso view (lids + walls).
/// </summary>
public sealed class MapView3D : IMapView
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
    private int _atlasTex;
    private int _vertexCount;
    private TileAtlas? _atlas;
    private CmpMap? _map;
    private G24StyleData? _style;

    public Vector3 Eye { get; set; } = new(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f + 50f, 30f);

    public Vector2 CameraWorld
    {
        get => new(Eye.X, Eye.Y);
        set => Eye = new Vector3(value.X, value.Y, Eye.Z);
    }
    /// <summary>Heading in radians (around Z, 0 = looking along +X).</summary>
    public float Yaw { get; set; } = -MathF.PI / 2f;
    /// <summary>Pitch in radians (around camera's right axis, 0 = horizontal, negative looks down).</summary>
    public float Pitch { get; set; } = -MathF.PI / 4f;

    public float FieldOfViewDegrees { get; set; } = 60f;
    public float MoveSpeed { get; set; } = 32f;        // tiles per second
    public float LookSensitivity { get; set; } = 0.005f;

    public float ViewportWidth { get; private set; } = 1;
    public float ViewportHeight { get; private set; } = 1;

    public (int x, int y, int z)? Selection { get; set; }
    public bool UsesFlyCamera => true;

    public MapView3D()
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
        GL.ClearColor(0.05f, 0.07f, 0.12f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_vertexCount == 0) return;

        var mvp = BuildMvp();
        _shader.Use();
        GL.UniformMatrix4(_shader.GetUniform("uMvp"), false, ref mvp);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _atlasTex);
        GL.Uniform1(_shader.GetUniform("uTex"), 0);
        GL.BindVertexArray(_vao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
    }

    public void Resize(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
        GL.Viewport(0, 0, width, height);
    }

    public void Pan(float screenDx, float screenDy)
    {
        // In 3D, panning isn't really a thing — strafe by translating along
        // camera-local right/up vectors so middle-drag still feels productive.
        var (forward, right, up) = Basis();
        float scale = 0.05f * MoveSpeed;
        Eye -= right * (screenDx * scale * 0.01f) + up * (-screenDy * scale * 0.01f);
    }

    public void Zoom(float factor, int anchorX, int anchorY)
    {
        // Dolly along forward axis.
        var (forward, _, _) = Basis();
        float step = MoveSpeed * 0.25f;
        Eye += forward * (factor > 1f ? step : -step);
    }

    public void ResetView()
    {
        Eye = new Vector3(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f + 50f, 30f);
        Yaw = -MathF.PI / 2f;
        Pitch = -MathF.PI / 4f;
    }

    public void FitMap() => ResetView();
    public void NativeZoom() => ResetView();

    /// <summary>
    /// Ray-cast from eye through the screen pixel onto the z=0 ground plane.
    /// Works fine for empty tiles; tiles with elevation get sampled by the
    /// (x,y) of the ray crossing z=0 even if a higher block visually covers them.
    /// </summary>
    public (int x, int y)? PickTile(int screenX, int screenY)
    {
        var mvp = BuildMvp();
        Matrix4 inv;
        try { inv = Matrix4.Invert(mvp); }
        catch { return null; }

        float ndcX = (screenX / ViewportWidth) * 2f - 1f;
        float ndcY = 1f - (screenY / ViewportHeight) * 2f;
        var nearW = Vector4.TransformRow(new Vector4(ndcX, ndcY, -1, 1), inv);
        var farW  = Vector4.TransformRow(new Vector4(ndcX, ndcY,  1, 1), inv);
        var n = nearW.Xyz / nearW.W;
        var f = farW.Xyz / farW.W;
        if (Math.Abs(f.Z - n.Z) < 1e-5f) return null;
        float t = -n.Z / (f.Z - n.Z);
        if (t < 0 || t > 1) return null;
        var hit = n + (f - n) * t;
        int tx = (int)Math.Floor(hit.X);
        int ty = (int)Math.Floor(hit.Y);
        if (tx < 0 || tx >= GameConfig.MapWidth || ty < 0 || ty >= GameConfig.MapHeight) return null;
        return (tx, ty);
    }

    public void ApplyLookDelta(float dx, float dy)
    {
        Yaw += dx * LookSensitivity;
        Pitch = Math.Clamp(Pitch - dy * LookSensitivity,
            MathHelper.DegreesToRadians(-89f),
            MathHelper.DegreesToRadians(89f));
    }

    public void Tick(float dt, bool fwd, bool back, bool left, bool right, bool up, bool down)
    {
        var (forward, rightV, upV) = Basis();
        float step = MoveSpeed * dt;
        if (fwd)   Eye += forward * step;
        if (back)  Eye -= forward * step;
        if (left)  Eye -= rightV  * step;
        if (right) Eye += rightV  * step;
        if (up)    Eye += Vector3.UnitZ * step;
        if (down)  Eye -= Vector3.UnitZ * step;
    }

    public void Dispose()
    {
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

    private (Vector3 forward, Vector3 right, Vector3 up) Basis()
    {
        var forward = new Vector3(
            MathF.Cos(Yaw) * MathF.Cos(Pitch),
            MathF.Sin(Yaw) * MathF.Cos(Pitch),
            MathF.Sin(Pitch));
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));
        return (forward, right, up);
    }

    private Matrix4 BuildMvp()
    {
        var (forward, _, up) = Basis();
        var view = Matrix4.LookAt(Eye, Eye + forward, up);
        float aspect = ViewportWidth / ViewportHeight;
        var proj = Matrix4.CreatePerspectiveFieldOfView(
            MathHelper.DegreesToRadians(FieldOfViewDegrees),
            Math.Max(aspect, 0.01f),
            0.1f, 2000f);
        return view * proj;
    }
}
