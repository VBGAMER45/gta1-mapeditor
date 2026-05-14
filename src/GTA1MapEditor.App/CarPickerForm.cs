using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.App;

/// <summary>
/// Modal: choose a car_info entry, HLS remap (0-11), and rotation for the
/// next PlaceCar click. Shows a live sprite preview using the car's
/// computed global sprite index. Layout uses TableLayoutPanel so neither
/// pane collapses regardless of form size.
/// </summary>
public sealed class CarPickerForm : Form
{
    public CarTemplate Result { get; private set; }

    private readonly G24StyleData _style;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly NumericUpDown _remap = new() { Minimum = 0, Maximum = 11, Width = 80 };
    private readonly NumericUpDown _rotation = new() { Minimum = 0, Maximum = 1023, Width = 80 };
    private readonly Label _details = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(6, 6, 6, 0), TextAlign = ContentAlignment.TopLeft };
    private readonly PixelPictureBox _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30) };
    private Bitmap? _previewBitmap;

    public CarPickerForm(G24StyleData style, CarTemplate initial)
    {
        _style = style;
        Text = "Pick Car";
        Width = 980;
        Height = 580;
        MinimumSize = new Size(720, 480);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        Result = new CarTemplate { Type = initial.Type, Remap = initial.Remap, Rotation = initial.Rotation };

        for (int i = 0; i < style.Cars.Count; i++)
        {
            var c = style.Cars[i];
            var cls = (VehicleClass)c.VehicleType;
            _list.Items.Add($"#{i}  {cls}  model={c.ModelId}  {c.Width}×{c.Height}  v={c.MaxSpeed}");
        }
        if (initial.Type < _list.Items.Count) _list.SelectedIndex = initial.Type;
        else if (_list.Items.Count > 0) _list.SelectedIndex = 0;

        _remap.Value = initial.Remap;
        _rotation.Value = initial.Rotation;

        _list.SelectedIndexChanged += (_, _) => UpdatePreview();
        _remap.ValueChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        Controls.Add(BuildLayout());
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(_list, 0, 0);

        var rightPane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        rightPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightPane.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        rightPane.Controls.Add(_preview, 0, 0);
        rightPane.Controls.Add(_details, 0, 1);
        root.Controls.Add(rightPane, 1, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 60, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8, 4, 0, 0) };
        fields.Controls.Add(LabeledRow("Remap (0-11):", _remap));
        fields.Controls.Add(LabeledRow("Rotation (0-1023):", _rotation));
        bottom.Controls.Add(fields, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        ok.Click += (_, _) =>
        {
            Result = new CarTemplate
            {
                Type = (byte)Math.Max(0, _list.SelectedIndex),
                Remap = (byte)_remap.Value,
                Rotation = (ushort)_rotation.Value,
            };
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        bottom.Controls.Add(buttons, 1, 0);
        AcceptButton = ok;
        CancelButton = cancel;

        root.Controls.Add(bottom, 0, 1);
        root.SetColumnSpan(bottom, 2);
        return root;
    }

    private void UpdatePreview()
    {
        int i = _list.SelectedIndex;
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        _preview.Image = null;
        if (i < 0 || i >= _style.Cars.Count) { _details.Text = ""; return; }

        var c = _style.Cars[i];
        int spriteIdx = SpriteRenderer.GetCarSpriteIndex(_style, c);
        _details.Text =
            $"Car #{i}    Class: {(VehicleClass)c.VehicleType}    Model: {c.ModelId}    Sprite #{spriteIdx}\n" +
            $"Dim: {c.Width}×{c.Height}×{c.Depth}    Speed: {c.MaxSpeed}    Mass: {c.Mass:F1}    Doors: {c.Doors.Count}";

        var rgba = SpriteRenderer.DecodeSprite(_style, spriteIdx, out int w, out int h);
        if (rgba is null) return;
        _previewBitmap = TilePickerForm_Helpers.RgbaToBitmap(rgba, w, h);
        _preview.Image = _previewBitmap;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _previewBitmap?.Dispose();
        base.Dispose(disposing);
    }

    private static Control LabeledRow(string text, Control field)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
        p.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(0, 4, 8, 0), Width = 140 });
        p.Controls.Add(field);
        return p;
    }
}
