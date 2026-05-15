using System.Diagnostics;

namespace GTA1MapEditor.App;

/// <summary>Small modal showing the app name, author credit, and a clickable GitHub link.</summary>
public sealed class AboutForm : Form
{
    private const string RepoUrl = "https://github.com/VBGAMER45/gta1-mapeditor";

    public AboutForm()
    {
        Text = "About GTA1 Map Editor";
        Width = 420;
        Height = 220;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16),
        };
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label
        {
            Text = "GTA1 Map Editor",
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
        });
        layout.Controls.Add(new Label
        {
            Text = "by vbgamer45",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        });

        var link = new LinkLabel
        {
            Text = RepoUrl,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Couldn't open browser",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        layout.Controls.Add(link);

        layout.Controls.Add(new Label
        {
            Text = "C# + WinForms + OpenTK · reads / writes .CMP via .G24",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, 12),
        });

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
        var okHost = new Panel { Dock = DockStyle.Fill };
        okHost.Controls.Add(ok);
        ok.Location = new Point(okHost.ClientSize.Width - ok.Width, okHost.ClientSize.Height - ok.Height);
        okHost.Resize += (_, _) => ok.Location = new Point(okHost.ClientSize.Width - ok.Width, okHost.ClientSize.Height - ok.Height);
        layout.Controls.Add(okHost);
        AcceptButton = ok;
        CancelButton = ok;

        Controls.Add(layout);
    }
}
