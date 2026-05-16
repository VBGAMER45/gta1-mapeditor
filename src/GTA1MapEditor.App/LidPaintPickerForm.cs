using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

/// <summary>
/// Modeless palette that lets the user pick a specific lid texture for the
/// PaintLid brush. Mirrors the layout of <see cref="TilePickerForm"/> but
/// stays open across clicks and live-binds the chosen lid via
/// <see cref="LidChosen"/>. Cell 0 paints "no texture" (clears the lid).
/// </summary>
public sealed class LidPaintPickerForm : Form
{
    /// <summary>1-based section-relative lid index (matches BlockInfo.Lid); 0 = no texture.</summary>
    public int SelectedLid { get; private set; }

    /// <summary>Brush rotation in 0-3 quarter-turn steps.</summary>
    public int SelectedRotation { get; private set; }

    /// <summary>Fires every time the user clicks a cell.</summary>
    public event Action<int>? LidChosen;

    /// <summary>Fires when the rotation control changes.</summary>
    public event Action<int>? RotationChosen;

    private readonly Panel _scroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly ToolStripStatusLabel _statusLabel = new() { Text = "" };
    private readonly RadioButton[] _rotRadios = new RadioButton[4];
    private Bitmap _atlasBitmap;
    private int _sectionStart;
    private int _sectionCount;
    private bool _suppressRotationEvent;
    private const int CellSize = 48;
    private const int Columns = 16;

    public LidPaintPickerForm(TileAtlas atlas, int sideCount, int lidCount, int initialLid, int initialRotation = 0)
    {
        Text = "Lid Brush";
        Width = Columns * CellSize + 64;
        Height = 640;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = new Point(Math.Max(0, work.Right - Width - 20), work.Top + 80);

        SelectedLid = initialLid;
        SelectedRotation = initialRotation & 3;
        _sectionStart = sideCount;
        _sectionCount = lidCount;
        _atlasBitmap = TilePickerForm_Helpers.AtlasToBitmap(atlas);

        // Rotation toolbar across the top — 0°/90°/180°/270° quarter-turn picks,
        // applied to the block's TypeMap rotation field when painting.
        var rotBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(6, 4, 6, 4),
        };
        rotBar.Controls.Add(new Label { Text = "Rotation:", AutoSize = true, Margin = new Padding(0, 4, 8, 0) });
        string[] labels = { "0°", "90°", "180°", "270°" };
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            var rb = new RadioButton
            {
                Text = labels[i], AutoSize = true,
                Appearance = Appearance.Button,
                Margin = new Padding(2),
                MinimumSize = new Size(48, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Checked = i == SelectedRotation,
            };
            rb.CheckedChanged += (_, _) =>
            {
                if (_suppressRotationEvent || !rb.Checked) return;
                SelectedRotation = idx;
                RotationChosen?.Invoke(idx);
            };
            _rotRadios[i] = rb;
            rotBar.Controls.Add(rb);
        }
        Controls.Add(rotBar);

        _scroll.Paint += DrawGrid;
        _scroll.MouseClick += OnPick;
        int gridCells = 1 + _sectionCount;
        _scroll.AutoScrollMinSize = new Size(Columns * CellSize,
            ((gridCells + Columns - 1) / Columns) * CellSize);
        Controls.Add(_scroll);

        var status = new StatusStrip();
        _statusLabel.Text = SelectedLid == 0 ? "Lid 0 (no texture)" : $"Lid {SelectedLid}";
        status.Items.Add(_statusLabel);
        Controls.Add(status);
        _scroll.MouseMove += (_, e) =>
        {
            int tile = HitTest(e.Location);
            if (tile >= 0)
                _statusLabel.Text = tile == 0 ? "Lid 0 (no texture)" : $"Lid {tile}";
        };
    }

    /// <summary>Rebind to a fresh atlas + lid counts (when the user opens a different map).</summary>
    public void SetAtlas(TileAtlas atlas, int sideCount, int lidCount)
    {
        _atlasBitmap.Dispose();
        _atlasBitmap = TilePickerForm_Helpers.AtlasToBitmap(atlas);
        _sectionStart = sideCount;
        _sectionCount = lidCount;
        int gridCells = 1 + _sectionCount;
        _scroll.AutoScrollMinSize = new Size(Columns * CellSize,
            ((gridCells + Columns - 1) / Columns) * CellSize);
        _scroll.Invalidate();
    }

    /// <summary>Update the highlighted cell without firing LidChosen (used when the brush is set externally, e.g., by right-click sampling).</summary>
    public void SyncSelection(int lidIndex)
    {
        if (SelectedLid == lidIndex) return;
        SelectedLid = lidIndex;
        _statusLabel.Text = lidIndex == 0 ? "Lid 0 (no texture)" : $"Lid {lidIndex}";
        _scroll.Invalidate();
    }

    /// <summary>Update the checked rotation radio without firing RotationChosen.</summary>
    public void SyncRotation(int rotation)
    {
        rotation &= 3;
        if (SelectedRotation == rotation) return;
        SelectedRotation = rotation;
        _suppressRotationEvent = true;
        try { _rotRadios[rotation].Checked = true; }
        finally { _suppressRotationEvent = false; }
    }

    private void DrawGrid(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.Clear(Color.FromArgb(30, 30, 30));

        int gridCells = 1 + _sectionCount;
        for (int i = 0; i < gridCells; i++)
        {
            int cellX = (i % Columns) * CellSize;
            int cellY = (i / Columns) * CellSize;
            var dst = new Rectangle(cellX + 1, cellY + 1, CellSize - 2, CellSize - 2);

            if (i == 0)
            {
                using var hatch = new System.Drawing.Drawing2D.HatchBrush(
                    System.Drawing.Drawing2D.HatchStyle.DiagonalCross, Color.DimGray, Color.Black);
                g.FillRectangle(hatch, dst);
                g.DrawString("0", Font, Brushes.White, dst.X + 4, dst.Y + 4);
            }
            else
            {
                int atlasTile = _sectionStart + (i - 1);
                int atlasX = (atlasTile % TileAtlas.Columns) * TileAtlas.CellSize + TileAtlas.Padding;
                int atlasY = (atlasTile / TileAtlas.Columns) * TileAtlas.CellSize + TileAtlas.Padding;
                var src = new Rectangle(atlasX, atlasY, TileAtlas.TileSize, TileAtlas.TileSize);
                g.DrawImage(_atlasBitmap, dst, src, GraphicsUnit.Pixel);
            }

            if (i == SelectedLid)
            {
                using var pen = new Pen(Color.Yellow, 2);
                g.DrawRectangle(pen, dst);
            }
        }
    }

    private void OnPick(object? sender, MouseEventArgs e)
    {
        int tile = HitTest(e.Location);
        if (tile < 0) return;
        SelectedLid = tile;
        _statusLabel.Text = tile == 0 ? "Lid 0 (no texture)" : $"Lid {tile}";
        _scroll.Invalidate();
        LidChosen?.Invoke(tile);
    }

    private int HitTest(Point loc)
    {
        var pt = new Point(loc.X - _scroll.AutoScrollPosition.X, loc.Y - _scroll.AutoScrollPosition.Y);
        int col = pt.X / CellSize;
        int row = pt.Y / CellSize;
        if (col < 0 || col >= Columns) return -1;
        int cell = row * Columns + col;
        int gridCells = 1 + _sectionCount;
        return cell >= 0 && cell < gridCells ? cell : -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _atlasBitmap.Dispose();
        base.Dispose(disposing);
    }
}
