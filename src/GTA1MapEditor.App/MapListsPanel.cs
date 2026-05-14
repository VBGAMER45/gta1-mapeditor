using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Commands;
using GTA1MapEditor.Core.Models;
using GTA1MapEditor.Rendering;
using OpenTK.Mathematics;

namespace GTA1MapEditor.App;

/// <summary>
/// Right-docked panel with five tabs (Objects, Cars, Routes, Sectors,
/// Spawns) listing every entity of that kind in the open map. Selecting
/// an item pans the camera to it; Delete removes it (with undo support).
/// </summary>
public sealed class MapListsPanel : UserControl
{
    private readonly EditorState _state;
    private readonly Func<MapView2D?> _topDownAccessor;

    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly ListBox _objectList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _carList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _routeList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _sectorList = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly ListBox _spawnList = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    public MapListsPanel(EditorState state, Func<MapView2D?> topDownAccessor)
    {
        _state = state;
        _topDownAccessor = topDownAccessor;
        Width = 260;
        Dock = DockStyle.Right;
        BackColor = SystemColors.Control;

        _tabs.TabPages.Add(BuildTab("Objects", _objectList, OnDeleteObject, OnGoToObject));
        _tabs.TabPages.Add(BuildTab("Cars", _carList, OnDeleteCar, OnGoToCar));
        _tabs.TabPages.Add(BuildTab("Routes", _routeList, OnDeleteRoute, OnGoToRoute));
        _tabs.TabPages.Add(BuildTab("Sectors", _sectorList, OnDeleteSector, OnGoToSector));
        _tabs.TabPages.Add(BuildTab("Spawns", _spawnList, OnDeleteSpawn, OnGoToSpawn));
        Controls.Add(_tabs);

        _state.MapLoaded += Refresh;
        _state.MapEdited += Refresh;
    }

    private TabPage BuildTab(string title, ListBox list, Action onDelete, Action onGoTo)
    {
        var page = new TabPage(title);
        page.Controls.Add(list);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, FlowDirection = FlowDirection.LeftToRight };
        var del = new Button { Text = "Delete", Width = 80 };
        del.Click += (_, _) => onDelete();
        bottom.Controls.Add(del);
        var go = new Button { Text = "Go to", Width = 80 };
        go.Click += (_, _) => onGoTo();
        bottom.Controls.Add(go);
        page.Controls.Add(bottom);
        list.DoubleClick += (_, _) => onGoTo();
        return page;
    }

    public new void Refresh()
    {
        var map = _state.Map;
        _objectList.BeginUpdate(); _objectList.Items.Clear();
        if (map is not null) foreach (var o in map.Objects)
            _objectList.Items.Add($"obj  type={o.Type}  ({o.X / GameConfig.TileSize},{o.Y / GameConfig.TileSize})  rot={o.Rotation}");
        _objectList.EndUpdate();

        _carList.BeginUpdate(); _carList.Items.Clear();
        if (map is not null) foreach (var c in map.CarPositions)
            _carList.Items.Add($"car  type={c.Type}  remap={c.Remap}  ({c.X / GameConfig.TileSize},{c.Y / GameConfig.TileSize})");
        _carList.EndUpdate();

        _routeList.BeginUpdate(); _routeList.Items.Clear();
        if (map is not null) for (int i = 0; i < map.Routes.Count; i++)
        {
            var r = map.Routes[i];
            string kind = r.Type == 255 ? "patrol" : $"roadblock#{r.Type}";
            _routeList.Items.Add($"route {i}  {kind}  {r.Vertices.Count}v");
        }
        _routeList.EndUpdate();

        _sectorList.BeginUpdate(); _sectorList.Items.Clear();
        if (map is not null) for (int i = 0; i < map.NavSectors.Count; i++)
        {
            var n = map.NavSectors[i];
            _sectorList.Items.Add($"sector {i}  '{n.Name}'  ({n.X},{n.Y}) {n.W}x{n.H}  sam={n.Sam}");
        }
        _sectorList.EndUpdate();

        _spawnList.BeginUpdate(); _spawnList.Items.Clear();
        if (map is not null) for (int i = 0; i < map.SpawnLocations.Count; i++)
        {
            var s = map.SpawnLocations[i];
            _spawnList.Items.Add($"{s.Type}  ({s.X},{s.Y},{s.Z})");
        }
        _spawnList.EndUpdate();
    }

    // ─── Actions ─────────────────────────────────────────────────────────

    private void OnDeleteObject()
    {
        if (_state.Map is null) return;
        int i = _objectList.SelectedIndex;
        if (i < 0 || i >= _state.Map.Objects.Count) return;
        _state.ExecuteCommand(new RemoveObjectCommand(_state.Map.Objects[i]));
    }

    private void OnDeleteCar()
    {
        if (_state.Map is null) return;
        int i = _carList.SelectedIndex;
        if (i < 0 || i >= _state.Map.CarPositions.Count) return;
        _state.ExecuteCommand(new RemoveCarCommand(_state.Map.CarPositions[i]));
    }

    private void OnDeleteRoute() => MessageBox.Show(this, "Route deletion not yet implemented.", "TODO");
    private void OnDeleteSector() => MessageBox.Show(this, "Sector deletion not yet implemented.", "TODO");

    private void OnDeleteSpawn()
    {
        if (_state.Map is null) return;
        int i = _spawnList.SelectedIndex;
        if (i < 0 || i >= _state.Map.SpawnLocations.Count) return;
        _state.ExecuteCommand(new RemoveSpawnCommand(_state.Map.SpawnLocations[i]));
    }

    private void OnGoToObject()
    {
        if (_state.Map is null) return;
        int i = _objectList.SelectedIndex;
        if (i < 0 || i >= _state.Map.Objects.Count) return;
        var o = _state.Map.Objects[i];
        PanTo(o.X / (float)GameConfig.TileSize, o.Y / (float)GameConfig.TileSize);
    }

    private void OnGoToCar()
    {
        if (_state.Map is null) return;
        int i = _carList.SelectedIndex;
        if (i < 0 || i >= _state.Map.CarPositions.Count) return;
        var c = _state.Map.CarPositions[i];
        PanTo(c.X / (float)GameConfig.TileSize, c.Y / (float)GameConfig.TileSize);
    }

    private void OnGoToRoute()
    {
        if (_state.Map is null) return;
        int i = _routeList.SelectedIndex;
        if (i < 0 || i >= _state.Map.Routes.Count) return;
        var r = _state.Map.Routes[i];
        if (r.Vertices.Count == 0) return;
        // Pan to the centroid of the route vertices.
        float sx = 0, sy = 0;
        foreach (var v in r.Vertices) { sx += v.X; sy += v.Y; }
        PanTo(sx / r.Vertices.Count, sy / r.Vertices.Count);
    }

    private void OnGoToSector()
    {
        if (_state.Map is null) return;
        int i = _sectorList.SelectedIndex;
        if (i < 0 || i >= _state.Map.NavSectors.Count) return;
        var n = _state.Map.NavSectors[i];
        PanTo(n.X + n.W / 2f, n.Y + n.H / 2f);
    }

    private void OnGoToSpawn()
    {
        if (_state.Map is null) return;
        int i = _spawnList.SelectedIndex;
        if (i < 0 || i >= _state.Map.SpawnLocations.Count) return;
        var s = _state.Map.SpawnLocations[i];
        PanTo(s.X + 0.5f, s.Y + 0.5f);
    }

    private void PanTo(float tx, float ty)
    {
        var v = _topDownAccessor();
        if (v is null) return;
        v.CameraWorld = new Vector2(tx, ty);
        if (v.PixelsPerTile < 16f) v.PixelsPerTile = 16f;
        _state.SetSelection(new TileSelection((int)tx, (int)ty, 0));
    }
}
