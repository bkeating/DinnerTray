namespace DinnerTray;

internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About DinnerTray";
        ClientSize = new Size(380, 366);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(247, 246, 244);
        Font = new Font("Segoe UI", 10);
        using var iconBitmap = AppArtwork.Draw(64);
        Icon = AppArtwork.ToIcon(iconBitmap);

        var logo = new PictureBox { Image = AppArtwork.Draw(192), SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(142, 28, 96, 96) };
        Controls.Add(logo);
        AddLabel("DinnerTray", 141, 36, 23, FontStyle.Bold, Color.FromArgb(39, 37, 35));
        var version = typeof(AboutForm).Assembly.GetName().Version;
        AddLabel($"Version {version?.ToString(3)}", 184, 24, 10, FontStyle.Regular, Color.FromArgb(120, 117, 113));
        AddLabel("A little screen. A lovely distraction.", 225, 26, 10, FontStyle.Regular, Color.FromArgb(85, 81, 77));
        AddLabel("Built by The American Spreadsheet Company", 282, 35, 9, FontStyle.Regular, Color.FromArgb(120, 117, 113));
        var close = new Button { Text = "Close", DialogResult = DialogResult.Cancel, Location = new Point(-200, -200) };
        Controls.Add(close);
        CancelButton = close;
        FormClosed += (_, _) => { logo.Image?.Dispose(); Icon?.Dispose(); };
    }

    private void AddLabel(string text, int top, int height, float size, FontStyle style, Color color) =>
        Controls.Add(new Label { Text = text, Bounds = new Rectangle(12, top, 356, height), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", size, style), ForeColor = color });
}
