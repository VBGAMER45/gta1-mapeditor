using GTA1MapEditor.Core;

namespace GTA1MapEditor.App;

/// <summary>Tiny modal asking for a tile (X, Y) to jump the camera to.</summary>
public sealed class GoToTileForm : Form
{
    public int X { get; private set; }
    public int Y { get; private set; }

    private readonly NumericUpDown _x = new() { Minimum = 0, Maximum = GameConfig.MapWidth - 1, Width = 80 };
    private readonly NumericUpDown _y = new() { Minimum = 0, Maximum = GameConfig.MapHeight - 1, Width = 80 };

    public GoToTileForm(int initialX, int initialY)
    {
        Text = "Go to tile";
        Width = 260;
        Height = 160;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        MinimizeBox = false;
        MaximizeBox = false;

        _x.Value = Math.Clamp(initialX, 0, GameConfig.MapWidth - 1);
        _y.Value = Math.Clamp(initialY, 0, GameConfig.MapHeight - 1);

        var fields = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        fields.Controls.Add(LabeledRow("X (0-255):", _x));
        fields.Controls.Add(LabeledRow("Y (0-255):", _y));
        Controls.Add(fields);

        var ok = new Button { Text = "Go", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        ok.Click += (_, _) => { X = (int)_x.Value; Y = (int)_y.Value; };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 36, Padding = new Padding(6) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private static Control LabeledRow(string label, Control field)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
        p.Controls.Add(new Label { Text = label, AutoSize = true, Width = 80, Margin = new Padding(0, 4, 8, 0) });
        p.Controls.Add(field);
        return p;
    }
}
