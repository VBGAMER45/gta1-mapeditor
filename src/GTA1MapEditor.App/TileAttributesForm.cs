using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Commands;
using GTA1MapEditor.Core.Models;
using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

/// <summary>
/// Modeless property inspector for a single tile, modelled after Junction25's
/// Attributes dialog. Reflects the selected tile from <see cref="EditorState"/>
/// and pushes commits as <see cref="BlockEditCommand"/>s so they participate in
/// undo/redo. Loads the tile snapshot on selection change and ignores its own
/// programmatic UI updates while syncing.
/// </summary>
public sealed class TileAttributesForm : Form
{
    private readonly EditorState _state;

    // Cube type radios (in BlockType numeric order so SelectedIndex == enum value)
    private readonly RadioButton[] _cubeRadios;
    private readonly string[] _cubeNames = { "Air / No Floor", "Water", "Road", "Pavement", "Field", "Solid (Building)" };

    private readonly CheckBox _navUp = new() { Text = "Up (north-ok)", AutoSize = true };
    private readonly CheckBox _navDown = new() { Text = "Down (south-ok)", AutoSize = true };
    private readonly CheckBox _navLeft = new() { Text = "Left (west-ok)", AutoSize = true };
    private readonly CheckBox _navRight = new() { Text = "Right (east-ok)", AutoSize = true };

    private readonly CheckBox _flipLr = new() { Text = "Flip East ↔ West", AutoSize = true };
    private readonly CheckBox _flat = new() { Text = "Transparent / Flat", AutoSize = true };
    private readonly CheckBox _trafficLights = new() { Text = "Traffic Lights", AutoSize = true };
    private readonly CheckBox _rail = new() { Text = "Railway", AutoSize = true };
    private readonly CheckBox _railStart = new() { Text = "Rail start turn", AutoSize = true };
    private readonly CheckBox _railEnd = new() { Text = "Rail end turn", AutoSize = true };

    private readonly NumericUpDown _rotation = new() { Minimum = 0, Maximum = 3, Width = 60 };
    private readonly NumericUpDown _slope = new() { Minimum = 0, Maximum = 63, Width = 60 };
    private readonly NumericUpDown _remap = new() { Minimum = 0, Maximum = 3, Width = 60 };
    private readonly NumericUpDown _z = new() { Minimum = 0, Maximum = 5, Width = 60 };

    private readonly TileButton _btnLid;
    private readonly TileButton _btnNorth;
    private readonly TileButton _btnSouth;
    private readonly TileButton _btnEast;
    private readonly TileButton _btnWest;

    private readonly Label _coordLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

    private bool _suppressEvents;
    private BlockSnapshot _snapshotBeforeEdit;
    private BlockInfo? _currentBlock;

    public TileAttributesForm(EditorState state)
    {
        _state = state;
        Text = "Tile Attributes";
        Width = 540;
        Height = 540;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(Screen.PrimaryScreen?.WorkingArea.Right - 560 ?? 800, 80);

        // Each button is bound to its tile section so the picker filters
        // the grid and the value stored is the 1-based section-relative byte
        // (matching BlockInfo.Lid / .Left / etc).
        _btnLid   = new TileButton("Lid (top)", TileSection.Lid);
        _btnNorth = new TileButton("North", TileSection.Side);
        _btnSouth = new TileButton("South", TileSection.Side);
        _btnEast  = new TileButton("East", TileSection.Side);
        _btnWest  = new TileButton("West", TileSection.Side);

        _btnLid.Clicked   += () => PickTexture(_btnLid,   "Lid");
        _btnNorth.Clicked += () => PickTexture(_btnNorth, "North wall");
        _btnSouth.Clicked += () => PickTexture(_btnSouth, "South wall");
        _btnEast.Clicked  += () => PickTexture(_btnEast,  "East wall");
        _btnWest.Clicked  += () => PickTexture(_btnWest,  "West wall");

        _cubeRadios = new RadioButton[6];
        for (int i = 0; i < _cubeRadios.Length; i++)
            _cubeRadios[i] = new RadioButton { Text = _cubeNames[i], AutoSize = true };

        BuildLayout();
        WireEvents();

        _state.SelectionChanged += SyncFromSelection;
        _state.MapLoaded += SyncFromSelection;
        _state.MapEdited += SyncFromSelection;

        SyncFromSelection();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(8),
            AutoSize = false,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
        };
        left.Controls.Add(_coordLabel);
        left.Controls.Add(LabeledRow("Height (Z):", _z));

        // Texture pickers in cross layout
        var texGrid = new TableLayoutPanel { ColumnCount = 3, RowCount = 3, AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        texGrid.Controls.Add(new Label { Text = "" }, 0, 0);
        texGrid.Controls.Add(_btnNorth, 1, 0);
        texGrid.Controls.Add(new Label { Text = "" }, 2, 0);
        texGrid.Controls.Add(_btnWest, 0, 1);
        texGrid.Controls.Add(_btnLid, 1, 1);
        texGrid.Controls.Add(_btnEast, 2, 1);
        texGrid.Controls.Add(new Label { Text = "" }, 0, 2);
        texGrid.Controls.Add(_btnSouth, 1, 2);
        texGrid.Controls.Add(new Label { Text = "" }, 2, 2);
        left.Controls.Add(texGrid);

        left.Controls.Add(GroupRadios("Cube Type", _cubeRadios));
        left.Controls.Add(GroupChecks("Vehicle Direction (movement allowed)", _navUp, _navDown, _navLeft, _navRight));

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
        };
        right.Controls.Add(LabeledRow("Top Rotation (90°):", _rotation));
        right.Controls.Add(LabeledRow("Slope (0-44):", _slope));
        right.Controls.Add(LabeledRow("Brightness Remap (0-3):", _remap));
        right.Controls.Add(GroupChecks("Flags", _flipLr, _flat, _trafficLights));
        right.Controls.Add(GroupChecks("Railway", _rail, _railStart, _railEnd));

        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);
        Controls.Add(root);
    }

    private static Control LabeledRow(string label, Control field)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
        p.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 4, 8, 0) });
        p.Controls.Add(field);
        return p;
    }

    private static GroupBox GroupRadios(string title, params RadioButton[] radios)
    {
        var box = new GroupBox { Text = title, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(6, 14, 6, 6) };
        foreach (var r in radios) flow.Controls.Add(r);
        box.Controls.Add(flow);
        return box;
    }

    private static GroupBox GroupChecks(string title, params CheckBox[] checks)
    {
        var box = new GroupBox { Text = title, AutoSize = true, Margin = new Padding(0, 4, 0, 4) };
        var flow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(6, 14, 6, 6) };
        foreach (var c in checks) flow.Controls.Add(c);
        box.Controls.Add(flow);
        return box;
    }

    private void WireEvents()
    {
        // Z changes select a different tile slot but don't push a command.
        _z.ValueChanged += (_, _) =>
        {
            if (_suppressEvents || _state.Selection is not { } sel) return;
            _state.SetSelection(new TileSelection(sel.X, sel.Y, (int)_z.Value));
        };

        foreach (var rb in _cubeRadios) rb.CheckedChanged += OnFieldChanged;
        foreach (var cb in new[] { _navUp, _navDown, _navLeft, _navRight,
                                   _flipLr, _flat, _trafficLights,
                                   _rail, _railStart, _railEnd })
            cb.CheckedChanged += OnFieldChanged;

        _rotation.ValueChanged += OnFieldChanged;
        _slope.ValueChanged += OnFieldChanged;
        _remap.ValueChanged += OnFieldChanged;

        _btnLid.TileIndexChanged += OnFieldChanged;
        _btnNorth.TileIndexChanged += OnFieldChanged;
        _btnSouth.TileIndexChanged += OnFieldChanged;
        _btnEast.TileIndexChanged += OnFieldChanged;
        _btnWest.TileIndexChanged += OnFieldChanged;
    }

    private void SyncFromSelection()
    {
        if (_state.Map is null || _state.Selection is null)
        {
            _coordLabel.Text = "No tile selected";
            SetControlsEnabled(false);
            return;
        }
        var sel = _state.Selection.Value;
        var stack = _state.Map.GetBlockStack(sel.X, sel.Y);
        if (stack.Count == 0)
        {
            _coordLabel.Text = $"({sel.X},{sel.Y},{sel.Z}) — empty column";
            SetControlsEnabled(false);
            return;
        }
        int z = Math.Clamp(sel.Z, 0, stack.Count - 1);
        _currentBlock = stack[z];
        _snapshotBeforeEdit = BlockEditCommand.Capture(_currentBlock);
        _coordLabel.Text = $"({sel.X}, {sel.Y}, {z})  ·  stack height {stack.Count}";

        _suppressEvents = true;
        try
        {
            _z.Maximum = stack.Count - 1;
            _z.Value = z;
            _cubeRadios[(int)_currentBlock.BlockType].Checked = true;
            _navUp.Checked = _currentBlock.UpOk;
            _navDown.Checked = _currentBlock.DownOk;
            _navLeft.Checked = _currentBlock.LeftOk;
            _navRight.Checked = _currentBlock.RightOk;
            _flipLr.Checked = _currentBlock.FlipLeftRight;
            _flat.Checked = _currentBlock.IsFlat;
            _trafficLights.Checked = _currentBlock.TrafficLights;
            _rail.Checked = _currentBlock.Railway;
            _railStart.Checked = _currentBlock.RailStartTurn;
            _railEnd.Checked = _currentBlock.RailEndTurn;
            _rotation.Value = (int)_currentBlock.Rotation;
            _slope.Value = Math.Min(_currentBlock.SlopeType, _slope.Maximum);
            _remap.Value = _currentBlock.RemapIndex;
            _btnLid.TileIndex = _currentBlock.Lid;
            _btnNorth.TileIndex = _currentBlock.Top;
            _btnSouth.TileIndex = _currentBlock.Bottom;
            _btnEast.TileIndex = _currentBlock.Right;
            _btnWest.TileIndex = _currentBlock.Left;
            UpdateTilePreviews();
        }
        finally { _suppressEvents = false; }
        SetControlsEnabled(true);
    }

    private void UpdateTilePreviews()
    {
        var atlas = _state.Atlas;
        int sideCount = _state.Style?.SideTileCount ?? 0;
        int lidCount  = _state.Style?.LidTileCount ?? 0;
        _btnLid.SetAtlas(atlas, sideCount, lidCount);
        _btnNorth.SetAtlas(atlas, sideCount, lidCount);
        _btnSouth.SetAtlas(atlas, sideCount, lidCount);
        _btnEast.SetAtlas(atlas, sideCount, lidCount);
        _btnWest.SetAtlas(atlas, sideCount, lidCount);
    }

    private void OnFieldChanged(object? sender, EventArgs e)
    {
        if (_suppressEvents || _currentBlock is null || _state.Selection is null) return;
        var sel = _state.Selection.Value;

        // Build target snapshot from current control state, applied to a copy.
        var probe = _currentBlock.Clone();
        for (int i = 0; i < _cubeRadios.Length; i++)
            if (_cubeRadios[i].Checked) probe.BlockType = (BlockType)i;
        probe.UpOk = _navUp.Checked;
        probe.DownOk = _navDown.Checked;
        probe.LeftOk = _navLeft.Checked;
        probe.RightOk = _navRight.Checked;
        probe.FlipLeftRight = _flipLr.Checked;
        probe.IsFlat = _flat.Checked;
        probe.TrafficLights = _trafficLights.Checked;
        probe.Railway = _rail.Checked;
        probe.RailStartTurn = _railStart.Checked;
        probe.RailEndTurn = _railEnd.Checked;
        probe.Rotation = (BlockRotation)(int)_rotation.Value;
        probe.SlopeType = (byte)_slope.Value;
        probe.RemapIndex = (byte)_remap.Value;
        probe.Lid = (byte)_btnLid.TileIndex;
        probe.Top = (byte)_btnNorth.TileIndex;
        probe.Bottom = (byte)_btnSouth.TileIndex;
        probe.Right = (byte)_btnEast.TileIndex;
        probe.Left = (byte)_btnWest.TileIndex;

        var after = BlockEditCommand.Capture(probe);
        if (after.Equals(_snapshotBeforeEdit)) return;

        var cmd = new BlockEditCommand(sel.X, sel.Y, sel.Z, _snapshotBeforeEdit, after);
        _state.ExecuteCommand(cmd);

        // After Execute: editor has detached the tile; refresh our cached block ref + baseline.
        _currentBlock = _state.Editor!.GetEditableBlock(sel.X, sel.Y, sel.Z);
        _snapshotBeforeEdit = after;
    }

    private void PickTexture(TileButton button, string label)
    {
        if (_state.Atlas is null || _state.Style is null) return;
        int sideCount = _state.Style.SideTileCount;
        int lidCount  = _state.Style.LidTileCount;
        using var picker = new TilePickerForm(_state.Atlas, sideCount, lidCount,
            button.Section, button.TileIndex, $"Pick {label} texture");
        if (picker.ShowDialog(this) == DialogResult.OK)
            button.TileIndex = picker.SelectedSectionTile;
    }

    private void SetControlsEnabled(bool on)
    {
        foreach (Control c in Controls) SetEnabledRecursive(c, on);
    }

    private static void SetEnabledRecursive(Control root, bool on)
    {
        root.Enabled = on;
        foreach (Control c in root.Controls) SetEnabledRecursive(c, on);
    }

    /// <summary>Small swatch button showing a tile thumbnail; click opens the picker.</summary>
    private sealed class TileButton : Button
    {
        public event Action? Clicked;
        public event EventHandler? TileIndexChanged;

        /// <summary>The tile section this button targets (Lid for lids, Side for walls).</summary>
        public TileSection Section { get; }

        private int _tileIndex;
        private TileAtlas? _atlas;
        private int _sideCount;
        private int _lidCount;
        private Bitmap? _preview;

        /// <summary>1-based section-relative tile (the value stored in BlockInfo.Lid / .Left etc); 0 = no texture.</summary>
        public int TileIndex
        {
            get => _tileIndex;
            set
            {
                if (_tileIndex == value) return;
                _tileIndex = value;
                Text = $"#{value}";
                RebuildPreview();
                TileIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public TileButton(string toolTip, TileSection section)
        {
            Section = section;
            Width = 76;
            Height = 76;
            Margin = new Padding(2);
            ImageAlign = ContentAlignment.MiddleCenter;
            TextAlign = ContentAlignment.BottomCenter;
            TextImageRelation = TextImageRelation.ImageAboveText;
            Font = new Font(Font.FontFamily, 7);
            new ToolTip().SetToolTip(this, toolTip);
            Click += (_, _) => Clicked?.Invoke();
        }

        public void SetAtlas(TileAtlas? atlas, int sideCount, int lidCount)
        {
            _atlas = atlas;
            _sideCount = sideCount;
            _lidCount = lidCount;
            RebuildPreview();
        }

        private void RebuildPreview()
        {
            _preview?.Dispose();
            _preview = null;
            if (_atlas is null || _tileIndex <= 0)
            {
                Image = null;
                return;
            }
            // Convert section-relative byte → atlas index based on this button's section.
            int atlasTile = Section switch
            {
                TileSection.Side => _tileIndex - 1,
                TileSection.Lid  => _sideCount + _tileIndex - 1,
                TileSection.Aux  => _sideCount + _lidCount + _tileIndex - 1,
                _ => -1,
            };
            if (atlasTile < 0 || atlasTile >= _atlas.TileCount) { Image = null; return; }

            int atlasX = (atlasTile % TileAtlas.Columns) * TileAtlas.CellSize + TileAtlas.Padding;
            int atlasY = (atlasTile / TileAtlas.Columns) * TileAtlas.CellSize + TileAtlas.Padding;
            var bmp = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                using var atlasBmp = TilePickerForm_Helpers.AtlasToBitmap(_atlas);
                g.DrawImage(atlasBmp,
                    new Rectangle(0, 0, 48, 48),
                    new Rectangle(atlasX, atlasY, TileAtlas.TileSize, TileAtlas.TileSize),
                    GraphicsUnit.Pixel);
            }
            _preview = bmp;
            Image = _preview;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _preview?.Dispose();
            base.Dispose(disposing);
        }
    }
}

/// <summary>RGBA → BGRA bitmap conversion helpers shared by the pickers.</summary>
internal static class TilePickerForm_Helpers
{
    public static Bitmap AtlasToBitmap(TileAtlas atlas) =>
        RgbaToBitmap(atlas.Rgba, atlas.WidthPixels, atlas.HeightPixels);

    /// <summary>Build a Format32bppArgb bitmap from a tightly-packed RGBA byte array.</summary>
    public static Bitmap RgbaToBitmap(byte[] rgba, int width, int height)
    {
        var bmp = new Bitmap(width, height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        // OpenGL RGBA → GDI+ BGRA channel swap.
        var bgra = new byte[rgba.Length];
        for (int i = 0; i + 3 < rgba.Length; i += 4)
        {
            bgra[i + 0] = rgba[i + 2];
            bgra[i + 1] = rgba[i + 1];
            bgra[i + 2] = rgba[i + 0];
            bgra[i + 3] = rgba[i + 3];
        }
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
        bmp.UnlockBits(data);
        return bmp;
    }
}
