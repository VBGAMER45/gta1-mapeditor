using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GTA1MapEditor.Rendering;

/// <summary>
/// Top-down orthographic renderer. Renders only the topmost lid per tile,
/// plus selection highlight + entity markers and (optionally) traffic-flag
/// arrows as a separate solid-colour overlay pass.
/// </summary>
public sealed class MapView2D : IMapView
{
    private const string TileVertexShader = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec2 aUv;
        uniform mat4 uProj;
        out vec2 vUv;
        void main()
        {
            gl_Position = uProj * vec4(aPos, 0.0, 1.0);
            vUv = aUv;
        }
        """;

    private const string TileFragmentShader = """
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

    private const string OverlayVertexShader = """
        #version 330 core
        layout(location = 0) in vec2 aPos;
        layout(location = 1) in vec4 aColor;
        uniform mat4 uProj;
        out vec4 vColor;
        void main()
        {
            gl_Position = uProj * vec4(aPos, 0.0, 1.0);
            vColor = aColor;
        }
        """;

    private const string OverlayFragmentShader = """
        #version 330 core
        in vec4 vColor;
        out vec4 fragColor;
        void main() { fragColor = vColor; }
        """;

    private readonly GlShader _tileShader;
    private readonly GlShader _overlayShader;
    private int _tileVao, _tileVbo;
    private int _overlayVao, _overlayVbo;
    private int _spriteVao, _spriteVbo;
    private int _atlasTex;
    private int _tileVertexCount;
    private int _overlayVertexCount;
    private TileAtlas? _atlas;
    private CmpMap? _map;
    private G24StyleData? _style;
    private SpriteCache? _spriteCache;
    private readonly float[] _spriteScratch = new float[24]; // 6 vertices * (x,y,u,v)

    public float ViewportWidth { get; set; } = 1;
    public float ViewportHeight { get; set; } = 1;
    public Vector2 CameraWorld { get; set; } = new(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f);
    public float PixelsPerTile { get; set; } = 32f;

    public const float MinZoom = 1f;
    public const float MaxZoom = 128f;
    public const float NativePixelsPerTile = 64f;

    public (int x, int y, int z)? Selection { get; set; }

    /// <summary>Render traffic-direction arrows on each block flagged as walkable/drivable.</summary>
    public bool ShowTrafficArrows { get; set; }

    public bool UsesFlyCamera => false;

    public MapView2D()
    {
        _tileShader = new GlShader(TileVertexShader, TileFragmentShader);
        _overlayShader = new GlShader(OverlayVertexShader, OverlayFragmentShader);

        _tileVao = GL.GenVertexArray();
        _tileVbo = GL.GenBuffer();
        GL.BindVertexArray(_tileVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _tileVbo);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 16, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 16, 8);

        _overlayVao = GL.GenVertexArray();
        _overlayVbo = GL.GenBuffer();
        GL.BindVertexArray(_overlayVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _overlayVbo);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 24, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 24, 8);

        // Sprite VAO uses the same (pos.xy, uv.xy) layout as the tile mesh.
        _spriteVao = GL.GenVertexArray();
        _spriteVbo = GL.GenBuffer();
        GL.BindVertexArray(_spriteVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _spriteVbo);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 16, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 16, 8);

        _atlasTex = GL.GenTexture();
    }

    public void SetMap(CmpMap map, G24StyleData style)
    {
        _map = map;
        _style = style;
        _atlas = TileAtlas.Build(style);

        _spriteCache?.Dispose();
        _spriteCache = new SpriteCache(style);

        AtlasTexture.Upload(_atlasTex, _atlas);
        RebuildMesh();
    }

    public TileAtlas? Atlas => _atlas;

    public void RebuildMesh()
    {
        if (_map is null || _atlas is null) { _tileVertexCount = 0; return; }

        var verts = new List<float>(GameConfig.MapWidth * GameConfig.MapHeight * 24);
        for (int y = 0; y < GameConfig.MapHeight; y++)
        for (int x = 0; x < GameConfig.MapWidth; x++)
        {
            var top = FindTopLid(_map, x, y);
            if (top is null) continue;
            int lid = top.Lid;
            if (lid <= 0 || lid >= _atlas.TileCount) continue;

            var uv = _atlas.GetUv(lid);
            float x0 = x, y0 = y, x1 = x + 1, y1 = y + 1;
            var nw = new Vector2(uv.u0, uv.v0);
            var ne = new Vector2(uv.u1, uv.v0);
            var se = new Vector2(uv.u1, uv.v1);
            var sw = new Vector2(uv.u0, uv.v1);
            if (top.FlipLeftRight) { (nw, ne) = (ne, nw); (sw, se) = (se, sw); }
            for (int r = 0; r < (int)top.Rotation; r++)
                (nw, ne, se, sw) = (sw, nw, ne, se);

            AddTileVert(verts, x0, y0, nw);
            AddTileVert(verts, x1, y0, ne);
            AddTileVert(verts, x1, y1, se);
            AddTileVert(verts, x0, y0, nw);
            AddTileVert(verts, x1, y1, se);
            AddTileVert(verts, x0, y1, sw);
        }
        _tileVertexCount = verts.Count / 4;
        GL.BindBuffer(BufferTarget.ArrayBuffer, _tileVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Count * sizeof(float),
            verts.ToArray(), BufferUsageHint.StaticDraw);
    }

    private void RebuildOverlay()
    {
        var verts = new List<float>(256);

        if (Selection is { } sel && sel.x >= 0 && sel.x < GameConfig.MapWidth
            && sel.y >= 0 && sel.y < GameConfig.MapHeight)
        {
            AddQuad(verts, sel.x, sel.y, sel.x + 1, sel.y + 1, 1f, 0.95f, 0.2f, 0.35f);
            float t = 0.08f;
            AddQuad(verts, sel.x, sel.y, sel.x + 1, sel.y + t,            1f, 0.95f, 0.2f, 1f);
            AddQuad(verts, sel.x, sel.y + 1 - t, sel.x + 1, sel.y + 1,    1f, 0.95f, 0.2f, 1f);
            AddQuad(verts, sel.x, sel.y, sel.x + t, sel.y + 1,            1f, 0.95f, 0.2f, 1f);
            AddQuad(verts, sel.x + 1 - t, sel.y, sel.x + 1, sel.y + 1,    1f, 0.95f, 0.2f, 1f);
        }

        if (_map is not null)
        {
            // Objects and cars are drawn as actual sprites in RenderEntitySprites();
            // only spawn locations need a coloured marker because no sprite is assigned
            // to them by the engine.
            foreach (var sp in _map.SpawnLocations)
            {
                float fx = sp.X + 0.5f;
                float fy = sp.Y + 0.5f;
                (float r, float g, float b) col = sp.Type switch
                {
                    SpawnLocationType.Police => (0.3f, 0.6f, 1f),
                    SpawnLocationType.Hospital => (1f, 1f, 1f),
                    SpawnLocationType.Fire => (1f, 0.4f, 0.1f),
                    _ => (1f, 1f, 1f),
                };
                AddQuad(verts, fx - 0.4f, fy - 0.4f, fx + 0.4f, fy + 0.4f, col.r, col.g, col.b, 1f);
            }

            if (ShowTrafficArrows)
                EmitTrafficArrows(verts);
        }

        _overlayVertexCount = verts.Count / 6;
        GL.BindBuffer(BufferTarget.ArrayBuffer, _overlayVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, verts.Count * sizeof(float),
            verts.ToArray(), BufferUsageHint.DynamicDraw);
    }

    /// <summary>
    /// Draw tiny arrows on each tile whose top block carries any of the
    /// UP/DOWN/LEFT/RIGHT nav flags. Cyan arrows = movement allowed in that
    /// compass direction.
    /// </summary>
    private void EmitTrafficArrows(List<float> verts)
    {
        if (_map is null) return;
        const float r = 0.18f;  // arrow size in tiles
        const float thick = 0.05f;
        for (int y = 0; y < GameConfig.MapHeight; y++)
        for (int x = 0; x < GameConfig.MapWidth; x++)
        {
            var top = FindTopLid(_map, x, y);
            if (top is null) continue;
            if (top.BlockType != BlockType.Road && top.BlockType != BlockType.Pavement) continue;
            float cx = x + 0.5f, cy = y + 0.5f;

            if (top.UpOk)    AddQuad(verts, cx - thick, cy - r,    cx + thick, cy,        0.2f, 0.9f, 1f, 0.9f);
            if (top.DownOk)  AddQuad(verts, cx - thick, cy,        cx + thick, cy + r,    0.2f, 0.9f, 1f, 0.9f);
            if (top.LeftOk)  AddQuad(verts, cx - r,     cy - thick, cx,         cy + thick, 0.2f, 0.9f, 1f, 0.9f);
            if (top.RightOk) AddQuad(verts, cx,         cy - thick, cx + r,     cy + thick, 0.2f, 0.9f, 1f, 0.9f);
        }
    }

    public void Render()
    {
        GL.Disable(EnableCap.DepthTest);
        GL.ClearColor(0.06f, 0.06f, 0.09f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        float halfW = ViewportWidth * 0.5f / PixelsPerTile;
        float halfH = ViewportHeight * 0.5f / PixelsPerTile;
        var proj = Matrix4.CreateOrthographicOffCenter(
            CameraWorld.X - halfW, CameraWorld.X + halfW,
            CameraWorld.Y + halfH, CameraWorld.Y - halfH,
            -1, 1);

        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        if (_tileVertexCount > 0)
        {
            _tileShader.Use();
            GL.UniformMatrix4(_tileShader.GetUniform("uProj"), false, ref proj);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _atlasTex);
            GL.Uniform1(_tileShader.GetUniform("uTex"), 0);
            GL.BindVertexArray(_tileVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _tileVertexCount);
        }

        // Entity sprites (cars, then objects). One draw per entity since each
        // sprite has its own texture; counts are bounded by map content so this
        // stays comfortably fast for editing.
        RenderEntitySprites(proj);

        RebuildOverlay();
        if (_overlayVertexCount > 0)
        {
            _overlayShader.Use();
            GL.UniformMatrix4(_overlayShader.GetUniform("uProj"), false, ref proj);
            GL.BindVertexArray(_overlayVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, _overlayVertexCount);
        }
    }

    private void RenderEntitySprites(Matrix4 proj)
    {
        if (_map is null || _spriteCache is null || _style is null) return;

        _tileShader.Use();
        GL.UniformMatrix4(_tileShader.GetUniform("uProj"), false, ref proj);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.Uniform1(_tileShader.GetUniform("uTex"), 0);
        GL.BindVertexArray(_spriteVao);

        // Cars first so objects appear on top of them when overlapping
        // (matches in-engine z-order for parking lots / props on roads).
        foreach (var car in _map.CarPositions)
        {
            if (car.Type >= _style.Cars.Count) continue;
            int spriteIdx = SpriteRenderer.GetCarSpriteIndex(_style, _style.Cars[car.Type]);
            DrawEntitySprite(spriteIdx, car.X, car.Y, car.Rotation);
        }
        foreach (var obj in _map.Objects)
        {
            if (obj.Type >= _style.Objects.Count) continue;
            int spriteIdx = _style.Objects[obj.Type].BaseSprite;
            DrawEntitySprite(spriteIdx, obj.X, obj.Y, obj.Rotation);
        }
    }

    /// <summary>
    /// Draw one sprite as a rotated, world-space-anchored quad centered on
    /// (worldX, worldY). Sprite pixels translate 1:1 to game pixels, so a
    /// 32×64 car sprite covers half a tile by one tile.
    /// </summary>
    private void DrawEntitySprite(int spriteIndex, ushort worldX, ushort worldY, ushort rotation)
    {
        var sprite = _spriteCache!.Get(spriteIndex);
        if (sprite is null) return;

        float ts = GameConfig.TileSize;
        float cx = worldX / ts;
        float cy = worldY / ts;
        float halfW = sprite.Value.Width  * 0.5f / ts;
        float halfH = sprite.Value.Height * 0.5f / ts;

        // CMP rotation is a 10-bit fixed-point heading (0..1023 = full turn).
        float angle = rotation * MathF.Tau / 1024f;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        // Local-space corner offsets, rotated around the entity center.
        (float x, float y) Rot(float dx, float dy) =>
            (cx + dx * cos - dy * sin, cy + dx * sin + dy * cos);

        var (x0, y0) = Rot(-halfW, -halfH);
        var (x1, y1) = Rot( halfW, -halfH);
        var (x2, y2) = Rot( halfW,  halfH);
        var (x3, y3) = Rot(-halfW,  halfH);

        var v = _spriteScratch;
        v[ 0] = x0; v[ 1] = y0; v[ 2] = 0; v[ 3] = 0;
        v[ 4] = x1; v[ 5] = y1; v[ 6] = 1; v[ 7] = 0;
        v[ 8] = x2; v[ 9] = y2; v[10] = 1; v[11] = 1;
        v[12] = x0; v[13] = y0; v[14] = 0; v[15] = 0;
        v[16] = x2; v[17] = y2; v[18] = 1; v[19] = 1;
        v[20] = x3; v[21] = y3; v[22] = 0; v[23] = 1;

        GL.BindTexture(TextureTarget.Texture2D, sprite.Value.Texture);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _spriteVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, v.Length * sizeof(float), v, BufferUsageHint.DynamicDraw);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
    }

    public void Resize(int width, int height)
    {
        ViewportWidth = width;
        ViewportHeight = height;
        GL.Viewport(0, 0, width, height);
    }

    public Vector2 ScreenToWorld(float screenX, float screenY)
    {
        float halfW = ViewportWidth * 0.5f / PixelsPerTile;
        float halfH = ViewportHeight * 0.5f / PixelsPerTile;
        return new Vector2(
            CameraWorld.X - halfW + screenX / PixelsPerTile,
            CameraWorld.Y - halfH + screenY / PixelsPerTile);
    }

    public (int x, int y)? PickTile(int screenX, int screenY)
    {
        var w = ScreenToWorld(screenX, screenY);
        int tx = (int)Math.Floor(w.X);
        int ty = (int)Math.Floor(w.Y);
        if (tx < 0 || tx >= GameConfig.MapWidth || ty < 0 || ty >= GameConfig.MapHeight) return null;
        return (tx, ty);
    }

    public void Pan(float screenDx, float screenDy) =>
        CameraWorld = new Vector2(CameraWorld.X - screenDx / PixelsPerTile, CameraWorld.Y - screenDy / PixelsPerTile);

    public void Zoom(float factor, int anchorX, int anchorY)
    {
        var before = ScreenToWorld(anchorX, anchorY);
        PixelsPerTile = Math.Clamp(PixelsPerTile * factor, MinZoom, MaxZoom);
        var after = ScreenToWorld(anchorX, anchorY);
        CameraWorld += before - after;
    }

    public void ResetView()
    {
        PixelsPerTile = 32f;
        CameraWorld = new Vector2(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f);
    }

    public void NativeZoom() { PixelsPerTile = NativePixelsPerTile; }

    public void FitMap()
    {
        if (ViewportWidth <= 1 || ViewportHeight <= 1) return;
        float fitX = ViewportWidth / GameConfig.MapWidth;
        float fitY = ViewportHeight / GameConfig.MapHeight;
        PixelsPerTile = Math.Clamp(Math.Min(fitX, fitY) * 0.95f, MinZoom, MaxZoom);
        CameraWorld = new Vector2(GameConfig.MapWidth / 2f, GameConfig.MapHeight / 2f);
    }

    public void ApplyLookDelta(float dx, float dy) { /* not used in 2D */ }

    public void Tick(float dt, bool fwd, bool back, bool left, bool right, bool up, bool down) { /* not used in 2D */ }

    public void Dispose()
    {
        _spriteCache?.Dispose();
        _tileShader.Dispose();
        _overlayShader.Dispose();
        GL.DeleteBuffer(_tileVbo);
        GL.DeleteBuffer(_overlayVbo);
        GL.DeleteBuffer(_spriteVbo);
        GL.DeleteVertexArray(_tileVao);
        GL.DeleteVertexArray(_overlayVao);
        GL.DeleteVertexArray(_spriteVao);
        GL.DeleteTexture(_atlasTex);
    }

    private static void AddTileVert(List<float> buf, float x, float y, Vector2 uv)
    {
        buf.Add(x); buf.Add(y); buf.Add(uv.X); buf.Add(uv.Y);
    }

    private static void AddQuad(List<float> buf, float x0, float y0, float x1, float y1,
        float r, float g, float b, float a)
    {
        void V(float x, float y) { buf.Add(x); buf.Add(y); buf.Add(r); buf.Add(g); buf.Add(b); buf.Add(a); }
        V(x0, y0); V(x1, y0); V(x1, y1);
        V(x0, y0); V(x1, y1); V(x0, y1);
    }

    private static BlockInfo? FindTopLid(CmpMap map, int x, int y)
    {
        var stack = map.GetBlockStack(x, y);
        for (int i = stack.Count - 1; i >= 0; i--)
            if (stack[i].Lid != 0) return stack[i];
        return stack.Count > 0 ? stack[^1] : null;
    }
}
