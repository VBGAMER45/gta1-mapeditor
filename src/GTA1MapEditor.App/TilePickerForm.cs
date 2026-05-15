using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

/// <summary>
/// Modal dialog showing one tile section (Side / Lid / Aux) as a grid of
/// thumbnails. Returns a 1-based index inside that section, which is the
/// value stored in <c>BlockInfo.Lid</c> / <c>BlockInfo.Left</c> etc.
/// Cell 0 represents "no texture".
/// </summary>
public sealed class TilePickerForm : Form
{
    /// <summary>0 = no texture, 1..N = section tile number.</summary>
    public int SelectedSectionTile { get; private set; }

    private readonly Panel _scroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Bitmap _atlasBitmap;
    private readonly int _sectionStart;
    private readonly int _sectionCount;
    private const int CellSize = 48;
    private const int Columns = 16;

    public TilePickerForm(TileAtlas atlas, int sideCount, int lidCount,
        TileSection section, int initialSectionTile, string title)
    {
        Text = title;
        Width = Columns * CellSize + 64;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        SelectedSectionTile = initialSectionTile;

        (_sectionStart, _sectionCount) = section switch
        {
            TileSection.Side => (0, sideCount),
            TileSection.Lid  => (sideCount, lidCount),
            TileSection.Aux  => (sideCount + lidCount, Math.Max(0, atlas.TileCount - sideCount - lidCount)),
            _ => (0, 0),
        };

        _atlasBitmap = AtlasToBitmap(atlas);
        _scroll.Paint += DrawGrid;
        _scroll.MouseClick += OnPick;
        // Grid is "no texture" cell (value 0) + sectionCount tile cells.
        int gridCells = 1 + _sectionCount;
        _scroll.AutoScrollMinSize = new Size(Columns * CellSize,
            ((gridCells + Columns - 1) / Columns) * CellSize);
        Controls.Add(_scroll);

        var status = new StatusStrip();
        var lbl = new ToolStripStatusLabel { Text = $"{section} {initialSectionTile}" };
        status.Items.Add(lbl);
        Controls.Add(status);
        _scroll.MouseMove += (_, e) =>
        {
            int tile = HitTest(e.Location);
            if (tile >= 0) lbl.Text = tile == 0 ? $"{section} 0 (no texture)" : $"{section} {tile}";
        };
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
                // "No texture" cell — chequered pattern + ∅ marker.
                using var hatch = new System.Drawing.Drawing2D.HatchBrush(
                    System.Drawing.Drawing2D.HatchStyle.DiagonalCross, Color.DimGray, Color.Black);
                g.FillRectangle(hatch, dst);
                using var pen2 = new Pen(Color.LightGray, 1);
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

            if (i == SelectedSectionTile)
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
        SelectedSectionTile = tile;
        DialogResult = DialogResult.OK;
        Close();
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

    private static Bitmap AtlasToBitmap(TileAtlas atlas)
    {
        var bmp = new Bitmap(atlas.WidthPixels, atlas.HeightPixels,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        var bgra = new byte[atlas.Rgba.Length];
        for (int i = 0; i + 3 < atlas.Rgba.Length; i += 4)
        {
            bgra[i + 0] = atlas.Rgba[i + 2];
            bgra[i + 1] = atlas.Rgba[i + 1];
            bgra[i + 2] = atlas.Rgba[i + 0];
            bgra[i + 3] = atlas.Rgba[i + 3];
        }
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
        bmp.UnlockBits(data);
        return bmp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _atlasBitmap.Dispose();
        base.Dispose(disposing);
    }
}
