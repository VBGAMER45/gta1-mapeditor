using GTA1MapEditor.Core;
using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.App;

/// <summary>
/// Picker for object_info entry, remap, and rotation. Two flavours:
/// <list type="bullet">
///   <item><description><b>Modal</b> (default): OK/Cancel buttons; caller reads <see cref="Result"/>. Used for editing a specific object instance.</description></item>
///   <item><description><b>Modeless</b> palette: set <see cref="LiveApply"/> before showing. Selection and field edits fire the callback live so the user can keep clicking the map between picks. Buttons collapse to a single Close.</description></item>
/// </list>
/// Layout uses TableLayoutPanel so neither pane ever collapses.
/// </summary>
public sealed class ObjectPickerForm : Form
{
    public ObjectTemplate Result { get; private set; }

    /// <summary>When non-null, the form runs in modeless palette mode and fires this on every change.</summary>
    public Action<ObjectTemplate>? LiveApply { get; set; }

    private readonly G24StyleData _style;
    private readonly int _objectSpriteBase;
    private readonly bool _modeless;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly NumericUpDown _remap = new() { Minimum = 0, Maximum = 255, Width = 80 };
    private readonly NumericUpDown _rotation = new() { Minimum = 0, Maximum = 1023, Width = 80 };
    private readonly Label _details = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Padding = new Padding(6, 6, 6, 0), TextAlign = ContentAlignment.TopLeft };
    private readonly PixelPictureBox _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(30, 30, 30) };
    private Bitmap? _previewBitmap;
    private bool _ready;

    public ObjectPickerForm(G24StyleData style, ObjectTemplate initial, bool modeless = false)
    {
        _style = style;
        _objectSpriteBase = SpriteRenderer.CategoryBase(style, SpriteCategory.Object);
        _modeless = modeless;
        Text = modeless ? "Object Palette" : "Pick Object";
        Width = 980;
        Height = 560;
        MinimumSize = new Size(720, 460);
        if (modeless)
        {
            // Palette-style: floats over the main form, doesn't steal focus from the map.
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            var work = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            Location = new Point(Math.Max(0, work.Right - Width - 20), work.Top + 80);
        }
        else
        {
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
        }
        Result = new ObjectTemplate { Type = initial.Type, Remap = initial.Remap, Rotation = initial.Rotation };

        for (int i = 0; i < style.Objects.Count; i++)
        {
            var o = style.Objects[i];
            _list.Items.Add($"#{i}  {o.Width}×{o.Height}×{o.Depth}  sprite={o.BaseSprite}  weight={o.Weight}");
        }
        if (initial.Type < _list.Items.Count) _list.SelectedIndex = initial.Type;
        else if (_list.Items.Count > 0) _list.SelectedIndex = 0;

        _remap.Value = initial.Remap;
        _rotation.Value = initial.Rotation;

        _list.SelectedIndexChanged += (_, _) => { UpdatePreview(); FireLive(); };
        _remap.ValueChanged += (_, _) => FireLive();
        _rotation.ValueChanged += (_, _) => FireLive();
        UpdatePreview();

        Controls.Add(BuildLayout());
        _ready = true;
    }

    private void FireLive()
    {
        if (!_modeless || !_ready) return;
        Result = new ObjectTemplate
        {
            Type = (byte)Math.Max(0, _list.SelectedIndex),
            Remap = (byte)_remap.Value,
            Rotation = (ushort)_rotation.Value,
        };
        LiveApply?.Invoke(Result);
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Top-left: list
        root.Controls.Add(_list, 0, 0);

        // Top-right: preview (top) + details (bottom)
        var rightPane = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        rightPane.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightPane.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        rightPane.Controls.Add(_preview, 0, 0);
        rightPane.Controls.Add(_details, 0, 1);
        root.Controls.Add(rightPane, 1, 0);

        // Bottom row: fields on the left, OK/Cancel on the right (spans both columns)
        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, Height = 60, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(8, 4, 0, 0) };
        fields.Controls.Add(LabeledRow("Remap:", _remap));
        fields.Controls.Add(LabeledRow("Rotation (0-1023):", _rotation));
        bottom.Controls.Add(fields, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        if (_modeless)
        {
            var close = new Button { Text = "Close", Width = 80 };
            close.Click += (_, _) => Close();
            buttons.Controls.Add(close);
            CancelButton = close;
        }
        else
        {
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 80 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
            ok.Click += (_, _) =>
            {
                Result = new ObjectTemplate
                {
                    Type = (byte)Math.Max(0, _list.SelectedIndex),
                    Remap = (byte)_remap.Value,
                    Rotation = (ushort)_rotation.Value,
                };
            };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

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
        if (i < 0 || i >= _style.Objects.Count) { _details.Text = ""; return; }

        var o = _style.Objects[i];
        _details.Text = $"Object #{i}    BaseSprite #{o.BaseSprite}    Aux: {o.Aux}    Status: {o.Status}\n" +
                        $"Dimensions: {o.Width}×{o.Height}×{o.Depth} px    Weight: {o.Weight}";

        // object_info.BaseSprite is local to the Object category; the global
        // index must add the category base or the preview shows a different
        // sprite than the one MapView2D actually places.
        int globalSprite = _objectSpriteBase + o.BaseSprite;
        var rgba = SpriteRenderer.DecodeSprite(_style, globalSprite, out int w, out int h);
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
