using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Commands;
using GTA1MapEditor.Core.Models;
using GTA1MapEditor.Rendering;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;

namespace GTA1MapEditor.App;

/// <summary>
/// GLControl that hosts whichever <see cref="IMapView"/> matches the active
/// <see cref="EditorState.View"/>. Top-down and iso share the same pan/zoom
/// idioms; 3D handles right-drag mouse-look + WASD via the held-key set.
/// </summary>
public sealed class MapViewControl : GLControl
{
    private readonly EditorState _state;
    private IMapView? _view;
    private Point _lastDragPoint;
    private bool _panning;
    private bool _painting;
    private bool _looking;

    // Entity drag state — set on left-down over an object/car in Select mode.
    private MapObject? _dragObj;
    private CarPosition? _dragCar;
    private ushort _dragOrigX, _dragOrigY;

    /// <summary>Raised when the user right-clicks an entity in Select mode. The main form opens a property dialog.</summary>
    public event Action<MapObject>? ObjectEditRequested;
    public event Action<CarPosition>? CarEditRequested;

    /// <summary>Fired when the active renderer's camera moves or zooms — scrollbars use this to follow along.</summary>
    public event Action? CameraChanged;

    /// <summary>Re-entrancy guard so programmatic camera updates (from scrollbars) don't recurse.</summary>
    private bool _suppressCameraEvent;

    private readonly System.Windows.Forms.Timer _flyTimer = new() { Interval = 16 };
    private readonly HashSet<Keys> _heldKeys = new();
    private DateTime _lastTick = DateTime.UtcNow;

    public event EventHandler<(int x, int y)>? HoveredTileChanged;

    public MapViewControl(EditorState state)
        : base(new GLControlSettings { APIVersion = new Version(3, 3) })
    {
        _state = state;
        Dock = DockStyle.Fill;

        _state.MapLoaded += () => { EnsureRendererForViewMode(); LoadMapIntoActiveView(); };
        _state.MapEdited += () => { _view?.RebuildMesh(); Invalidate(); };
        _state.SelectionChanged += () =>
        {
            if (_view is null) return;
            _view.Selection = _state.Selection is { } sel ? (sel.X, sel.Y, sel.Z) : null;
            Invalidate();
        };
        _state.ViewModeChanged += OnViewModeChanged;
        _state.OverlaysChanged += () =>
        {
            if (_view is MapView2D v2) v2.ShowTrafficArrows = _state.ShowTrafficArrows;
            Invalidate();
        };
        _state.MapEdited += () =>
        {
            // ShowGroundLevel changes need a mesh rebuild since the tile mesh
            // is baked, not picked per frame.
            if (_view is MapView2D v2 && v2.ShowGroundLevel != _state.ShowGroundLevel)
            {
                v2.ShowGroundLevel = _state.ShowGroundLevel;
                v2.RebuildMesh();
            }
        };

        _flyTimer.Tick += OnFlyTick;
    }

    public IMapView? View => _view;

    public void ZoomIn()    { _view?.Zoom(1.25f, ClientSize.Width / 2, ClientSize.Height / 2); RaiseCamera(); Invalidate(); }
    public void ZoomOut()   { _view?.Zoom(1f / 1.25f, ClientSize.Width / 2, ClientSize.Height / 2); RaiseCamera(); Invalidate(); }
    public void ResetZoom() { _view?.ResetView(); RaiseCamera(); Invalidate(); }
    public void NativeZoom(){ _view?.NativeZoom(); RaiseCamera(); Invalidate(); }
    public void FitMap()    { _view?.FitMap(); RaiseCamera(); Invalidate(); }

    /// <summary>Programmatically move the camera (e.g., from scrollbars or Go-to-tile) without triggering CameraChanged.</summary>
    public void SetCameraWorld(float worldX, float worldY)
    {
        if (_view is null) return;
        _suppressCameraEvent = true;
        try { _view.CameraWorld = new OpenTK.Mathematics.Vector2(worldX, worldY); }
        finally { _suppressCameraEvent = false; }
        Invalidate();
    }

    private void RaiseCamera()
    {
        if (_suppressCameraEvent) return;
        CameraChanged?.Invoke();
    }

    // ─── Renderer lifecycle ───────────────────────────────────────────────

    private void OnViewModeChanged()
    {
        _heldKeys.Clear();
        _flyTimer.Stop();
        if (_view is not null)
        {
            MakeCurrent();
            _view.Dispose();
            _view = null;
        }
        EnsureRendererForViewMode();
        LoadMapIntoActiveView();
        if (_view?.UsesFlyCamera == true) _flyTimer.Start();
        Invalidate();
    }

    private void EnsureRendererForViewMode()
    {
        if (_view is not null) return;
        if (!IsHandleCreated) return;
        MakeCurrent();
        _view = _state.View switch
        {
            ViewMode.TopDown => new MapView2D { ShowTrafficArrows = _state.ShowTrafficArrows },
            ViewMode.Isometric => new MapViewIso(),
            ViewMode.Perspective3D => new MapView3D(),
            _ => new MapView2D(),
        };
    }

    private void LoadMapIntoActiveView()
    {
        if (_view is null || _state.Map is null || _state.Style is null) return;
        MakeCurrent();
        _view.SetMap(_state.Map, _state.Style);
        _view.Resize(ClientSize.Width, ClientSize.Height);
        _view.Selection = _state.Selection is { } sel ? (sel.X, sel.Y, sel.Z) : null;
        if (_view is MapView2D v2)
        {
            v2.ShowTrafficArrows = _state.ShowTrafficArrows;
            // Apply the user's ground/top preference before the first render.
            if (v2.ShowGroundLevel != _state.ShowGroundLevel)
            {
                v2.ShowGroundLevel = _state.ShowGroundLevel;
                v2.RebuildMesh();
            }
        }
        Invalidate();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MakeCurrent();
        GL.Enable(EnableCap.Blend);
        EnsureRendererForViewMode();
        if (_state.Map is not null) LoadMapIntoActiveView();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (!IsHandleCreated || _view is null) return;
        MakeCurrent();
        _view.Resize(ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_view is null) { base.OnPaint(e); return; }
        MakeCurrent();
        _view.Resize(ClientSize.Width, ClientSize.Height);
        _view.Render();
        SwapBuffers();
    }

    // ─── Input ────────────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_view is null) return;

        if (e.Button == MouseButtons.Middle)
        {
            _panning = true;
            _lastDragPoint = e.Location;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (_view.UsesFlyCamera && e.Button == MouseButtons.Right)
        {
            _looking = true;
            _lastDragPoint = e.Location;
            Cursor = Cursors.Cross;
            Focus();
            return;
        }

        // Top-down / iso: tile-based clicks.
        var pick = _view.PickTile(e.X, e.Y);
        if (pick is null) return;
        int tx = pick.Value.x, ty = pick.Value.y;

        if (e.Button == MouseButtons.Right) { HandleRightClick(tx, ty); return; }
        if (e.Button == MouseButtons.Left)  { HandleLeftDown(tx, ty);  return; }
    }

    private void HandleLeftDown(int tx, int ty)
    {
        switch (_state.Tool)
        {
            case ToolMode.Select:
                {
                    // First check if user clicked on an entity; that initiates a drag.
                    var hit = FindEntityNear(tx + 0.5f, ty + 0.5f, radius: 0.7f);
                    if (hit.obj is not null) { _dragObj = hit.obj; _dragOrigX = hit.obj.X; _dragOrigY = hit.obj.Y; return; }
                    if (hit.car is not null) { _dragCar = hit.car; _dragOrigX = hit.car.X; _dragOrigY = hit.car.Y; return; }
                    var stack = _state.Map?.GetBlockStack(tx, ty);
                    int topZ = stack is { Count: > 0 } ? stack.Count - 1 : 0;
                    _state.SetSelection(new TileSelection(tx, ty, topZ));
                    break;
                }
            case ToolMode.PaintLid:
                _painting = true;
                PaintLidAt(tx, ty);
                break;
            case ToolMode.PlaceObject:
                {
                    var t = _state.ObjectTemplate;
                    var obj = new MapObject
                    {
                        X = (ushort)((tx + 0.5f) * GameConfig.TileSize),
                        Y = (ushort)((ty + 0.5f) * GameConfig.TileSize),
                        Z = 0,
                        Type = t.Type,
                        Remap = t.Remap,
                        Rotation = t.Rotation,
                        Pitch = t.Pitch,
                        Roll = t.Roll,
                    };
                    _state.ExecuteCommand(new AddObjectCommand(obj));
                    break;
                }
            case ToolMode.PlaceCar:
                {
                    var t = _state.CarTemplate;
                    var car = new CarPosition
                    {
                        X = (ushort)((tx + 0.5f) * GameConfig.TileSize),
                        Y = (ushort)((ty + 0.5f) * GameConfig.TileSize),
                        Z = 0,
                        Type = t.Type,
                        Remap = t.Remap,
                        Rotation = t.Rotation,
                    };
                    _state.ExecuteCommand(new AddCarCommand(car));
                    break;
                }
            case ToolMode.PlaceSpawn:
                {
                    if (_state.Map is null) return;
                    // Respect the 6-per-type cap the engine enforces.
                    int existing = 0;
                    foreach (var s in _state.Map.SpawnLocations)
                        if (s.Type == _state.SpawnType) existing++;
                    if (existing >= 6)
                    {
                        MessageBox.Show(this, $"Already 6 {_state.SpawnType} spawn slots in use.",
                            "Slot limit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    var sp = new Core.Models.SpawnLocation
                    {
                        X = (byte)tx, Y = (byte)ty, Z = 0, Type = _state.SpawnType,
                    };
                    _state.ExecuteCommand(new Core.Commands.AddSpawnCommand(sp));
                    break;
                }
            case ToolMode.Erase:
                EraseNearest(tx, ty);
                break;
        }
    }

    private void HandleRightClick(int tx, int ty)
    {
        switch (_state.Tool)
        {
            case ToolMode.PaintLid:
                {
                    var stack = _state.Map?.GetBlockStack(tx, ty);
                    if (stack is { Count: > 0 })
                    {
                        var top = stack[^1];
                        if (top.Lid > 0) _state.PaintTile = top.Lid;
                    }
                    break;
                }
            case ToolMode.PlaceObject:
            case ToolMode.PlaceCar:
            case ToolMode.PlaceSpawn:
            case ToolMode.Erase:
                EraseNearest(tx, ty);
                break;
            case ToolMode.Select:
                {
                    // Right-click on entity → open property dialog. Otherwise clear selection.
                    var hit = FindEntityNear(tx + 0.5f, ty + 0.5f, radius: 0.7f);
                    if (hit.obj is not null) ObjectEditRequested?.Invoke(hit.obj);
                    else if (hit.car is not null) CarEditRequested?.Invoke(hit.car);
                    else _state.SetSelection(null);
                    break;
                }
            default:
                _state.SetSelection(null);
                break;
        }
    }

    private (MapObject? obj, CarPosition? car) FindEntityNear(float worldX, float worldY, float radius)
    {
        if (_state.Map is null) return (null, null);
        float ts = GameConfig.TileSize;
        MapObject? bestObj = null; float bestObjDist = radius;
        CarPosition? bestCar = null; float bestCarDist = radius;
        foreach (var o in _state.Map.Objects)
        {
            float dx = o.X / ts - worldX, dy = o.Y / ts - worldY;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < bestObjDist) { bestObjDist = d; bestObj = o; }
        }
        foreach (var c in _state.Map.CarPositions)
        {
            float dx = c.X / ts - worldX, dy = c.Y / ts - worldY;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < bestCarDist) { bestCarDist = d; bestCar = c; }
        }
        // Return whichever is closer; tie-break to object.
        if (bestObj is not null && bestObjDist <= bestCarDist) return (bestObj, null);
        if (bestCar is not null) return (null, bestCar);
        return (null, null);
    }

    private void PaintLidAt(int tx, int ty)
    {
        if (_state.Editor is null || _state.Map is null) return;
        var stack = _state.Map.GetBlockStack(tx, ty);
        if (stack.Count == 0) return;
        int topZ = stack.Count - 1;
        for (int i = stack.Count - 1; i >= 0; i--)
            if (stack[i].Lid != 0) { topZ = i; break; }
        var before = BlockEditCommand.Capture(stack[topZ]);
        if (before.Lid == _state.PaintTile) return;
        var after = before with { Lid = (byte)_state.PaintTile };
        _state.ExecuteCommand(new BlockEditCommand(tx, ty, topZ, before, after));
    }

    private void EraseNearest(int tx, int ty)
    {
        if (_state.Map is null) return;
        float worldX = tx + 0.5f, worldY = ty + 0.5f;
        const float radius = 1.5f;
        MapObject? bestObj = null; float bestObjDist = radius;
        CarPosition? bestCar = null; float bestCarDist = radius;
        float ts = GameConfig.TileSize;
        foreach (var o in _state.Map.Objects)
        {
            float dx = o.X / ts - worldX, dy = o.Y / ts - worldY;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < bestObjDist) { bestObjDist = d; bestObj = o; }
        }
        foreach (var c in _state.Map.CarPositions)
        {
            float dx = c.X / ts - worldX, dy = c.Y / ts - worldY;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < bestCarDist) { bestCarDist = d; bestCar = c; }
        }
        if (bestObj is not null && bestObjDist <= bestCarDist)
            _state.ExecuteCommand(new RemoveObjectCommand(bestObj));
        else if (bestCar is not null)
            _state.ExecuteCommand(new RemoveCarCommand(bestCar));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_view is null) return;

        if (_looking)
        {
            int dx = e.X - _lastDragPoint.X;
            int dy = e.Y - _lastDragPoint.Y;
            _view.ApplyLookDelta(dx, dy);
            _lastDragPoint = e.Location;
            Invalidate();
            return;
        }
        if (_panning)
        {
            int dx = e.X - _lastDragPoint.X;
            int dy = e.Y - _lastDragPoint.Y;
            _view.Pan(dx, dy);
            _lastDragPoint = e.Location;
            RaiseCamera();
            Invalidate();
        }

        var pick = _view.PickTile(e.X, e.Y);
        if (pick is { } p)
        {
            HoveredTileChanged?.Invoke(this, (p.x, p.y));
            if (_painting && _state.Tool == ToolMode.PaintLid) PaintLidAt(p.x, p.y);

            // Live entity drag — update X/Y as the cursor moves.
            ushort wx = (ushort)((p.x + 0.5f) * GameConfig.TileSize);
            ushort wy = (ushort)((p.y + 0.5f) * GameConfig.TileSize);
            if (_dragObj is not null) { _dragObj.X = wx; _dragObj.Y = wy; Invalidate(); }
            else if (_dragCar is not null) { _dragCar.X = wx; _dragCar.Y = wy; Invalidate(); }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_panning && e.Button == MouseButtons.Middle) { _panning = false; Cursor = Cursors.Default; }
        if (_looking && e.Button == MouseButtons.Right) { _looking = false; Cursor = Cursors.Default; }
        if (_painting && e.Button == MouseButtons.Left) _painting = false;

        // Finalise entity drag: emit an undoable move command if the position
        // actually changed (so a click-without-drag doesn't pollute the stack).
        if (e.Button == MouseButtons.Left && _dragObj is not null)
        {
            var moved = _dragObj;
            ushort newX = moved.X, newY = moved.Y;
            if (newX != _dragOrigX || newY != _dragOrigY)
            {
                moved.X = _dragOrigX; moved.Y = _dragOrigY; // command's Do() will set new
                _state.ExecuteCommand(new MoveObjectCommand(moved, _dragOrigX, _dragOrigY, newX, newY));
            }
            _dragObj = null;
        }
        if (e.Button == MouseButtons.Left && _dragCar is not null)
        {
            var moved = _dragCar;
            ushort newX = moved.X, newY = moved.Y;
            if (newX != _dragOrigX || newY != _dragOrigY)
            {
                moved.X = _dragOrigX; moved.Y = _dragOrigY;
                _state.ExecuteCommand(new MoveCarCommand(moved, _dragOrigX, _dragOrigY, newX, newY));
            }
            _dragCar = null;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_view is null) return;
        float factor = e.Delta > 0 ? 1.25f : 1f / 1.25f;
        _view.Zoom(factor, e.X, e.Y);
        RaiseCamera();
        Invalidate();
    }

    // 3D fly cam --------------------------------------------------------------
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_view?.UsesFlyCamera == true) { _heldKeys.Add(e.KeyCode); RaiseCamera(); }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _heldKeys.Remove(e.KeyCode);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        // Capture WASD / QE so the form-level navigation doesn't swallow them.
        var k = keyData & Keys.KeyCode;
        return _view?.UsesFlyCamera == true && k is Keys.W or Keys.A or Keys.S or Keys.D or Keys.Q or Keys.E
            ? true
            : base.IsInputKey(keyData);
    }

    private void OnFlyTick(object? sender, EventArgs e)
    {
        if (_view is null || !_view.UsesFlyCamera) return;
        var now = DateTime.UtcNow;
        float dt = (float)(now - _lastTick).TotalSeconds;
        _lastTick = now;
        bool fwd   = _heldKeys.Contains(Keys.W);
        bool back  = _heldKeys.Contains(Keys.S);
        bool left  = _heldKeys.Contains(Keys.A);
        bool right = _heldKeys.Contains(Keys.D);
        bool up    = _heldKeys.Contains(Keys.E);
        bool down  = _heldKeys.Contains(Keys.Q);
        if (fwd || back || left || right || up || down)
        {
            _view.Tick(dt, fwd, back, left, right, up, down);
            RaiseCamera();
            Invalidate();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _flyTimer.Dispose();
            _view?.Dispose();
        }
        base.Dispose(disposing);
    }
}
