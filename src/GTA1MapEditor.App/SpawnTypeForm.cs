using GTA1MapEditor.Core.Models;

namespace GTA1MapEditor.App;

/// <summary>Tiny modal: pick which kind of spawn slot we're placing.</summary>
public sealed class SpawnTypeForm : Form
{
    public SpawnLocationType Result { get; private set; }

    public SpawnTypeForm(SpawnLocationType initial)
    {
        Text = "Spawn type";
        Width = 260;
        Height = 200;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        Result = initial;

        var rbPolice   = new RadioButton { Text = "Police",   AutoSize = true, Checked = initial == SpawnLocationType.Police };
        var rbHospital = new RadioButton { Text = "Hospital", AutoSize = true, Checked = initial == SpawnLocationType.Hospital };
        var rbFire     = new RadioButton { Text = "Fire",     AutoSize = true, Checked = initial == SpawnLocationType.Fire };

        var radios = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        radios.Controls.Add(rbPolice);
        radios.Controls.Add(rbHospital);
        radios.Controls.Add(rbFire);
        Controls.Add(radios);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 36, Padding = new Padding(6) };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 70 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 70 };
        ok.Click += (_, _) =>
        {
            if (rbHospital.Checked) Result = SpawnLocationType.Hospital;
            else if (rbFire.Checked) Result = SpawnLocationType.Fire;
            else Result = SpawnLocationType.Police;
        };
        bottom.Controls.Add(ok);
        bottom.Controls.Add(cancel);
        Controls.Add(bottom);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
