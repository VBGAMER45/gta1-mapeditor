using System.Drawing.Drawing2D;

namespace GTA1MapEditor.App;

/// <summary>
/// Picture box that draws its <see cref="System.Windows.Forms.Control.BackgroundImage"/>/Image with
/// integer-scaled nearest-neighbor sampling, preserving the chunky look of
/// GTA1's 64×64 sprites and tiles when displayed at a larger size.
/// </summary>
public sealed class PixelPictureBox : Control
{
    private Image? _image;
    public Image? Image
    {
        get => _image;
        set { _image = value; Invalidate(); }
    }

    public PixelPictureBox()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        if (_image is null) return;

        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;

        // Integer scale to fit the control while preserving aspect ratio.
        int srcW = _image.Width, srcH = _image.Height;
        if (srcW == 0 || srcH == 0) return;
        float scale = Math.Min((float)ClientSize.Width / srcW, (float)ClientSize.Height / srcH);
        // Snap to integer if zoomed in, otherwise use fractional scale.
        if (scale >= 1f) scale = MathF.Floor(scale);
        int dstW = (int)(srcW * scale);
        int dstH = (int)(srcH * scale);
        int x = (ClientSize.Width - dstW) / 2;
        int y = (ClientSize.Height - dstH) / 2;
        g.DrawImage(_image, new Rectangle(x, y, dstW, dstH),
            new Rectangle(0, 0, srcW, srcH), GraphicsUnit.Pixel);
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
}
