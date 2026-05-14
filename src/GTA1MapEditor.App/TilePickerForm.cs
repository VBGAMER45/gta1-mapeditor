using GTA1MapEditor.Rendering;

namespace GTA1MapEditor.App;

/// <summary>
/// Modal dialog showing every tile in the atlas as a clickable thumbnail.
/// Exposes the chosen tile index via <see cref="SelectedTile"/>.
/// </summary>
public sealed class TilePickerForm : Form
{
    public int SelectedTile { get; private set; }

    private readonly Panel _scroll = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Bitmap _atlasBitmap;
    private const int CellSize = 48;
    private const int Columns = 16;
    private readonly int _tileCount;

    public TilePickerForm(TileAtlas atlas, int initialTile, string title)
    {
        Text = title;
        Width = Columns * CellSize + 64;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        SelectedTile = initialTile;
        _tileCount = atlas.TileCount;

        _atlasBitmap = AtlasToBitmap(atlas);
        _scroll.Paint += DrawGrid;
        _scroll.MouseClick += OnPick;
        _scroll.AutoScrollMinSize = new Size(Columns * CellSize, ((_tileCount + Columns - 1) / Columns) * CellSize);
        Controls.Add(_scroll);

        var status = new StatusStrip();
        var lbl = new ToolStripStatusLabel { Text = $"Tile {initialTile}" };
        status.Items.Add(lbl);
        Controls.Add(status);
        _scroll.MouseMove += (_, e) =>
        {
            int tile = HitTest(e.Location);
            if (tile >= 0) lbl.Text = $"Tile {tile}";
        };
    }

    private void DrawGrid(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        g.Clear(Color.FromArgb(30, 30, 30));

        int rows = (_tileCount + Columns - 1) / Columns;
        for (int i = 0; i < _tileCount; i++)
        {
            int cellX = (i % Columns) * CellSize;
            int cellY = (i / Columns) * CellSize;
            int atlasX = (i % TileAtlas.Columns) * TileAtlas.CellSize + TileAtlas.Padding;
            int atlasY = (i / TileAtlas.Columns) * TileAtlas.CellSize + TileAtlas.Padding;
            var src = new Rectangle(atlasX, atlasY, TileAtlas.TileSize, TileAtlas.TileSize);
            var dst = new Rectangle(cellX + 1, cellY + 1, CellSize - 2, CellSize - 2);
            g.DrawImage(_atlasBitmap, dst, src, GraphicsUnit.Pixel);
            if (i == SelectedTile)
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
        SelectedTile = tile;
        DialogResult = DialogResult.OK;
        Close();
    }

    private int HitTest(Point loc)
    {
        var pt = new Point(loc.X - _scroll.AutoScrollPosition.X, loc.Y - _scroll.AutoScrollPosition.Y);
        int col = pt.X / CellSize;
        int row = pt.Y / CellSize;
        if (col < 0 || col >= Columns) return -1;
        int tile = row * Columns + col;
        return tile >= 0 && tile < _tileCount ? tile : -1;
    }

    private static Bitmap AtlasToBitmap(TileAtlas atlas)
    {
        var bmp = new Bitmap(atlas.WidthPixels, atlas.HeightPixels,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, bmp.PixelFormat);

        // OpenGL atlas is RGBA; GDI+ Format32bppArgb is BGRA. Build a temporary swapped buffer then memcpy.
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
