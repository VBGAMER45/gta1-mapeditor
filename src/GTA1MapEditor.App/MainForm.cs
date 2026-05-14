using System.ComponentModel;
using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Commands;
using GTA1MapEditor.Core.Models;
using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

public sealed class MainForm : Form
{
    private readonly EditorState _state = new();
    private readonly MapViewControl _viewControl;
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _hoverLabel = new() { Text = "—" };
    private readonly ToolStripStatusLabel _mapLabel = new() { Text = "No map loaded", Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel _toolLabel = new() { Text = "Tool: Select" };

    private ToolStripMenuItem _undoItem = null!;
    private ToolStripMenuItem _redoItem = null!;
    private ToolStripButton _undoBtn = null!;
    private ToolStripButton _redoBtn = null!;
    private ToolStripMenuItem _viewTopDownItem = null!;
    private ToolStripMenuItem _viewIsoItem = null!;
    private ToolStripMenuItem _view3DItem = null!;
    private ToolStripMenuItem _trafficArrowsItem = null!;

    private TileAttributesForm? _attributesForm;
    private readonly MapListsPanel _listsPanel;

    public MainForm()
    {
        Text = "GTA1 Map Editor";
        Width = 1380;
        Height = 800;
        MinimumSize = new Size(1000, 600);

        _viewControl = new MapViewControl(_state);
        _listsPanel = new MapListsPanel(_state, () => _viewControl.View as MapView2D);

        MainMenuStrip = BuildMenu();
        var toolbar = BuildToolbar();

        _statusStrip.Items.Add(_mapLabel);
        _statusStrip.Items.Add(_toolLabel);
        _statusStrip.Items.Add(_hoverLabel);

        Controls.Add(_viewControl);
        Controls.Add(_listsPanel);
        Controls.Add(toolbar);
        Controls.Add(MainMenuStrip);
        Controls.Add(_statusStrip);

        _viewControl.HoveredTileChanged += OnHovered;
        _viewControl.ObjectEditRequested += OpenObjectInstanceEditor;
        _viewControl.CarEditRequested += OpenCarInstanceEditor;
        _state.MapLoaded += () => { UpdateMapLabel(); UpdateCommandUi(); };
        _state.MapEdited += UpdateMapLabel;
        _state.CommandsChanged += UpdateCommandUi;
        _state.ToolChanged += OnToolChanged;
        _state.ViewModeChanged += UpdateViewModeUi;
        _state.OverlaysChanged += UpdateOverlayUi;

        FormClosing += OnFormClosing;
        KeyPreview = true;
        KeyDown += OnKeyDown;
    }

    private MenuStrip BuildMenu()
    {
        var menu = new MenuStrip { Dock = DockStyle.Top };

        var file = new ToolStripMenuItem("&File");
        file.DropDownItems.Add(MakeItem("&Open .CMP…", Keys.Control | Keys.O, OnOpenCmp));
        file.DropDownItems.Add(MakeItem("&Save", Keys.Control | Keys.S, OnSave));
        file.DropDownItems.Add(MakeItem("Save &As…", Keys.Control | Keys.Shift | Keys.S, OnSaveAs));
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(MakeItem("E&xit", Keys.Alt | Keys.F4, (_, _) => Close()));

        var edit = new ToolStripMenuItem("&Edit");
        _undoItem = MakeItem("&Undo", Keys.Control | Keys.Z, (_, _) => _state.Undo());
        _redoItem = MakeItem("&Redo", Keys.Control | Keys.Y, (_, _) => _state.Redo());
        edit.DropDownItems.Add(_undoItem);
        edit.DropDownItems.Add(_redoItem);

        var view = new ToolStripMenuItem("&View");
        view.DropDownItems.Add(MakeItem("Zoom &In", Keys.Control | Keys.Oemplus, (_, _) => _viewControl.ZoomIn()));
        view.DropDownItems.Add(MakeItem("Zoom &Out", Keys.Control | Keys.OemMinus, (_, _) => _viewControl.ZoomOut()));
        view.DropDownItems.Add(MakeItem("&Reset Zoom", Keys.Control | Keys.D0, (_, _) => _viewControl.ResetZoom()));
        view.DropDownItems.Add(MakeItem("&100% (Native style size)", Keys.Control | Keys.D9, (_, _) => _viewControl.NativeZoom()));
        view.DropDownItems.Add(MakeItem("&Fit Map", Keys.Control | Keys.F, (_, _) => _viewControl.FitMap()));
        view.DropDownItems.Add(new ToolStripSeparator());
        _viewTopDownItem = MakeItem("&Top-down 2D", Keys.Control | Keys.D1, (_, _) => _state.SetViewMode(ViewMode.TopDown));
        _viewIsoItem     = MakeItem("&Isometric 2.5D", Keys.Control | Keys.D2, (_, _) => _state.SetViewMode(ViewMode.Isometric));
        _view3DItem      = MakeItem("&3D Free-look", Keys.Control | Keys.D3, (_, _) => _state.SetViewMode(ViewMode.Perspective3D));
        _viewTopDownItem.Checked = true;
        view.DropDownItems.Add(_viewTopDownItem);
        view.DropDownItems.Add(_viewIsoItem);
        view.DropDownItems.Add(_view3DItem);
        view.DropDownItems.Add(new ToolStripSeparator());
        _trafficArrowsItem = MakeItem("Traffic &arrows overlay", Keys.F2, (_, _) => _state.ToggleTrafficArrows());
        view.DropDownItems.Add(_trafficArrowsItem);
        view.DropDownItems.Add(new ToolStripSeparator());
        view.DropDownItems.Add(MakeItem("Tile &Attributes…", Keys.F4, (_, _) => OpenAttributesPanel()));

        var tools = new ToolStripMenuItem("&Tools");
        // Single-letter hotkeys are handled in OnKeyDown — WinForms ShortcutKeys
        // requires a modifier for letter keys.
        tools.DropDownItems.Add(MakeItem("&Select (S)", Keys.None, (_, _) => _state.SetTool(ToolMode.Select)));
        tools.DropDownItems.Add(MakeItem("&Paint Lid (P)", Keys.None, (_, _) => _state.SetTool(ToolMode.PaintLid)));
        tools.DropDownItems.Add(MakeItem("Place &Object (O)…", Keys.None, (_, _) => EnterPlaceObjectTool()));
        tools.DropDownItems.Add(MakeItem("Place &Car (C)…", Keys.None, (_, _) => EnterPlaceCarTool()));
        tools.DropDownItems.Add(MakeItem("Place S&pawn (T)…", Keys.None, (_, _) => EnterPlaceSpawnTool()));
        tools.DropDownItems.Add(MakeItem("&Eraser (E)", Keys.None, (_, _) => _state.SetTool(ToolMode.Erase)));
        tools.DropDownItems.Add(new ToolStripSeparator());
        tools.DropDownItems.Add(MakeItem("Configure &Object template…", Keys.None, (_, _) => OpenObjectPicker()));
        tools.DropDownItems.Add(MakeItem("Configure C&ar template…", Keys.None, (_, _) => OpenCarPicker()));

        var help = new ToolStripMenuItem("&Help");
        help.DropDownItems.Add(MakeItem("&About", Keys.None, (_, _) =>
            MessageBox.Show(this, "GTA1 Map Editor\nC# + WinForms + OpenTK\nReads / writes .CMP via .G24",
                "About", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange(new ToolStripItem[] { file, edit, view, tools, help });
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        var bar = new ToolStrip { Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden };

        bar.Items.Add(new ToolStripButton("Open", null, OnOpenCmp));
        bar.Items.Add(new ToolStripButton("Save", null, OnSave));
        bar.Items.Add(new ToolStripSeparator());

        _undoBtn = new ToolStripButton("Undo", null, (_, _) => _state.Undo()) { Enabled = false };
        _redoBtn = new ToolStripButton("Redo", null, (_, _) => _state.Redo()) { Enabled = false };
        bar.Items.Add(_undoBtn);
        bar.Items.Add(_redoBtn);
        bar.Items.Add(new ToolStripSeparator());

        bar.Items.Add(new ToolStripButton("Zoom −", null, (_, _) => _viewControl.ZoomOut()));
        bar.Items.Add(new ToolStripButton("Zoom +", null, (_, _) => _viewControl.ZoomIn()));
        bar.Items.Add(new ToolStripButton("Fit", null, (_, _) => _viewControl.FitMap()));
        bar.Items.Add(new ToolStripSeparator());

        AddToolButton(bar, "Select", ToolMode.Select);
        AddToolButton(bar, "Paint", ToolMode.PaintLid);
        var objBtn = new ToolStripButton("Object", null, (_, _) => EnterPlaceObjectTool());
        _state.ToolChanged += () => objBtn.Checked = _state.Tool == ToolMode.PlaceObject;
        bar.Items.Add(objBtn);
        var carBtn = new ToolStripButton("Car", null, (_, _) => EnterPlaceCarTool());
        _state.ToolChanged += () => carBtn.Checked = _state.Tool == ToolMode.PlaceCar;
        bar.Items.Add(carBtn);
        AddToolButton(bar, "Erase", ToolMode.Erase);
        bar.Items.Add(new ToolStripSeparator());

        var attrBtn = new ToolStripButton("Attributes", null, (_, _) => OpenAttributesPanel());
        bar.Items.Add(attrBtn);
        var arrowsBtn = new ToolStripButton("Arrows", null, (_, _) => _state.ToggleTrafficArrows());
        _state.OverlaysChanged += () => arrowsBtn.Checked = _state.ShowTrafficArrows;
        bar.Items.Add(arrowsBtn);
        bar.Items.Add(new ToolStripSeparator());

        var vTop = new ToolStripButton("2D", null, (_, _) => _state.SetViewMode(ViewMode.TopDown)) { Checked = true };
        var vIso = new ToolStripButton("Iso", null, (_, _) => _state.SetViewMode(ViewMode.Isometric));
        var v3D = new ToolStripButton("3D", null, (_, _) => _state.SetViewMode(ViewMode.Perspective3D));
        _state.ViewModeChanged += () =>
        {
            vTop.Checked = _state.View == ViewMode.TopDown;
            vIso.Checked = _state.View == ViewMode.Isometric;
            v3D.Checked = _state.View == ViewMode.Perspective3D;
        };
        bar.Items.Add(vTop);
        bar.Items.Add(vIso);
        bar.Items.Add(v3D);

        return bar;
    }

    private void AddToolButton(ToolStrip bar, string label, ToolMode mode)
    {
        var btn = new ToolStripButton(label, null, (_, _) => _state.SetTool(mode))
        {
            CheckOnClick = false,
        };
        _state.ToolChanged += () => btn.Checked = _state.Tool == mode;
        bar.Items.Add(btn);
    }

    private static ToolStripMenuItem MakeItem(string text, Keys shortcut, EventHandler onClick)
    {
        var item = new ToolStripMenuItem(text, null, onClick);
        if (shortcut != Keys.None)
        {
            try { item.ShortcutKeys = shortcut; }
            catch (InvalidEnumArgumentException) { /* combination rejected by WinForms; show in label only */ }
        }
        return item;
    }

    // File menu -------------------------------------------------------------
    private void OnOpenCmp(object? sender, EventArgs e)
    {
        if (!ConfirmDiscardChanges()) return;

        using var dlg = new OpenFileDialog
        {
            Filter = "GTA1 map (*.CMP)|*.cmp|All files (*.*)|*.*",
            Title = "Open GTA1 .CMP",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var cmp = CmpReader.ReadFile(dlg.FileName);
            var stylePath = LocateStyleFor(dlg.FileName, cmp.Header.StyleNumber);
            if (stylePath is null)
            {
                MessageBox.Show(this,
                    $"Couldn't auto-locate style {cmp.Header.StyleNumber}. " +
                    "Place styleNNN.g24 next to the .CMP or in a sibling 'styles' folder.",
                    "Style missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var style = G24Reader.ReadFile(stylePath);
            _state.LoadMap(cmp, style, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Failed to open map",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (_state.Map is null) return;
        if (_state.FilePath is null) { OnSaveAs(sender, e); return; }
        try { _state.Save(); UpdateMapLabel(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void OnSaveAs(object? sender, EventArgs e)
    {
        if (_state.Map is null) return;
        using var dlg = new SaveFileDialog
        {
            Filter = "GTA1 map (*.CMP)|*.cmp",
            FileName = Path.GetFileName(_state.FilePath ?? "untitled.cmp"),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { _state.Save(dlg.FileName); UpdateMapLabel(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmDiscardChanges()) e.Cancel = true;
    }

    private bool ConfirmDiscardChanges()
    {
        if (!_state.IsDirty) return true;
        var r = MessageBox.Show(this,
            "You have unsaved changes. Save before continuing?",
            "Unsaved changes",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
        if (r == DialogResult.Cancel) return false;
        if (r == DialogResult.Yes) OnSave(this, EventArgs.Empty);
        return true;
    }

    private void OnToolChanged()
    {
        _toolLabel.Text = $"Tool: {_state.Tool}";
        if (_state.Tool == ToolMode.PlaceObject) _toolLabel.Text += $"  (#{_state.ObjectTemplate.Type})";
        else if (_state.Tool == ToolMode.PlaceCar) _toolLabel.Text += $"  (#{_state.CarTemplate.Type})";
    }

    private void UpdateViewModeUi()
    {
        _viewTopDownItem.Checked = _state.View == ViewMode.TopDown;
        _viewIsoItem.Checked     = _state.View == ViewMode.Isometric;
        _view3DItem.Checked      = _state.View == ViewMode.Perspective3D;
        // Traffic arrows only render in 2D top-down; grey them out elsewhere.
        _trafficArrowsItem.Enabled = _state.View == ViewMode.TopDown;
    }

    private void UpdateOverlayUi()
    {
        _trafficArrowsItem.Checked = _state.ShowTrafficArrows;
    }

    private void EnterPlaceObjectTool()
    {
        if (_state.Style is null) return;
        using var dlg = new ObjectPickerForm(_state.Style, _state.ObjectTemplate);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _state.ObjectTemplate = dlg.Result;
        _state.SetTool(ToolMode.PlaceObject);
    }

    private void EnterPlaceCarTool()
    {
        if (_state.Style is null) return;
        using var dlg = new CarPickerForm(_state.Style, _state.CarTemplate);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _state.CarTemplate = dlg.Result;
        _state.SetTool(ToolMode.PlaceCar);
    }

    private void EnterPlaceSpawnTool()
    {
        using var dlg = new SpawnTypeForm(_state.SpawnType);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _state.SpawnType = dlg.Result;
        _state.SetTool(ToolMode.PlaceSpawn);
    }

    private void OpenObjectInstanceEditor(MapObject obj)
    {
        if (_state.Style is null) return;
        var before = EditObjectCommand.Capture(obj);
        var current = new ObjectTemplate
        {
            Type = obj.Type, Remap = obj.Remap, Rotation = obj.Rotation,
            Pitch = obj.Pitch, Roll = obj.Roll,
        };
        using var dlg = new ObjectPickerForm(_state.Style, current) { Text = $"Edit object @ ({obj.X / GameConfig.TileSize},{obj.Y / GameConfig.TileSize})" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var t = dlg.Result;
        var after = new ObjectSnapshot(t.Type, t.Remap, t.Rotation, current.Pitch, current.Roll);
        if (after.Equals(before)) return;
        _state.ExecuteCommand(new EditObjectCommand(obj, before, after));
    }

    private void OpenCarInstanceEditor(CarPosition car)
    {
        if (_state.Style is null) return;
        var before = EditCarCommand.Capture(car);
        var current = new CarTemplate { Type = car.Type, Remap = car.Remap, Rotation = car.Rotation };
        using var dlg = new CarPickerForm(_state.Style, current) { Text = $"Edit car @ ({car.X / GameConfig.TileSize},{car.Y / GameConfig.TileSize})" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        var t = dlg.Result;
        var after = new CarSnapshot(t.Type, t.Remap, t.Rotation);
        if (after.Equals(before)) return;
        _state.ExecuteCommand(new EditCarCommand(car, before, after));
    }

    private void OpenObjectPicker()
    {
        if (_state.Style is null) return;
        using var dlg = new ObjectPickerForm(_state.Style, _state.ObjectTemplate);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _state.ObjectTemplate = dlg.Result;
    }

    private void OpenCarPicker()
    {
        if (_state.Style is null) return;
        using var dlg = new CarPickerForm(_state.Style, _state.CarTemplate);
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _state.CarTemplate = dlg.Result;
    }

    private void OpenAttributesPanel()
    {
        if (_state.Map is null) return;
        if (_attributesForm is null || _attributesForm.IsDisposed)
        {
            _attributesForm = new TileAttributesForm(_state);
            _attributesForm.FormClosed += (_, _) => _attributesForm = null;
            _attributesForm.Show(this);
        }
        else
        {
            _attributesForm.BringToFront();
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Zoom on Ctrl + numpad / shifted plus-minus
        if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
        {
            _viewControl.ZoomIn();
            e.Handled = true;
            return;
        }
        if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
        {
            _viewControl.ZoomOut();
            e.Handled = true;
            return;
        }

        // Single-letter tool hotkeys (only when no modifier and not typing in a focused text control).
        if (e.Control || e.Alt || e.Shift) return;
        if (ActiveControl is TextBox or NumericUpDown) return;

        switch (e.KeyCode)
        {
            case Keys.S: _state.SetTool(ToolMode.Select); e.Handled = true; break;
            case Keys.P: _state.SetTool(ToolMode.PaintLid); e.Handled = true; break;
            case Keys.O: EnterPlaceObjectTool(); e.Handled = true; break;
            case Keys.C: EnterPlaceCarTool(); e.Handled = true; break;
            case Keys.T: EnterPlaceSpawnTool(); e.Handled = true; break;
            case Keys.E: _state.SetTool(ToolMode.Erase); e.Handled = true; break;
        }
    }

    // Status / state helpers -----------------------------------------------
    private static string? LocateStyleFor(string cmpPath, byte styleNumber)
    {
        string styleName = $"style{styleNumber:D3}.g24";
        var cmpDir = Path.GetDirectoryName(cmpPath) ?? "";

        var candidates = new[]
        {
            Path.Combine(cmpDir, styleName),
            Path.Combine(cmpDir, "..", "styles", $"style{styleNumber:D3}", styleName),
            Path.Combine(cmpDir, "..", $"style{styleNumber:D3}", styleName),
            Path.Combine(cmpDir, "..", "styles", $"style{styleNumber:D3}", $"style{styleNumber:D3}.g24"),
        };
        foreach (var c in candidates)
        {
            string full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private void OnHovered(object? sender, (int x, int y) tile)
    {
        if (_state.Map is null) { _hoverLabel.Text = "—"; return; }
        if (tile.x < 0 || tile.x >= GameConfig.MapWidth || tile.y < 0 || tile.y >= GameConfig.MapHeight)
        {
            _hoverLabel.Text = $"({tile.x},{tile.y}) out";
            return;
        }
        var stack = _state.Map.GetBlockStack(tile.x, tile.y);
        var top = stack.Count > 0 ? stack[^1] : null;
        string blockDesc = top is null ? "empty" :
            $"{top.BlockType} slope:{top.SlopeType} rot:{top.Rotation} lid:{top.Lid}";
        _hoverLabel.Text = $"({tile.x:D3},{tile.y:D3}) h={stack.Count}  {blockDesc}";
    }

    private void UpdateMapLabel()
    {
        if (_state.Map is null) { _mapLabel.Text = "No map loaded"; return; }
        string dirty = _state.IsDirty ? "*" : "";
        string name = Path.GetFileName(_state.FilePath ?? "untitled.cmp");
        _mapLabel.Text =
            $"{name}{dirty}  ·  style {_state.Map.Header.StyleNumber}  ·  " +
            $"{_state.Map.Objects.Count} obj / {_state.Map.CarPositions.Count} car / " +
            $"{_state.Map.Routes.Count} rt / {_state.Map.NavSectors.Count} nav";
        Text = $"GTA1 Map Editor — {name}{dirty}";
    }

    private void UpdateCommandUi()
    {
        var s = _state.Commands;
        _undoItem.Enabled = _undoBtn.Enabled = s.CanUndo;
        _redoItem.Enabled = _redoBtn.Enabled = s.CanRedo;
        _undoItem.Text = s.CanUndo ? $"&Undo  {s.UndoDescription}" : "&Undo";
        _redoItem.Text = s.CanRedo ? $"&Redo  {s.RedoDescription}" : "&Redo";
        UpdateMapLabel();
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(this, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void NotImplemented(string what) =>
        MessageBox.Show(this, $"{what} — coming soon.", "Not implemented",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
}
