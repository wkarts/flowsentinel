namespace FlowSentinel.Desktop;

internal sealed class SplashForm : Form
{
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly Image? _developerLogo;

    internal SplashForm()
    {
        _developerLogo = DesktopAssets.LoadDeveloperLogo();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.White;
        ClientSize = new Size(650, 370);
        UseWaitCursor = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(28)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(5, 39, 83),
            Padding = new Padding(24)
        };

        var logo = new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = _developerLogo,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        logoPanel.Controls.Add(logo);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 7,
            Padding = new Padding(30, 28, 10, 18)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "FlowSentinel",
            AutoSize = true,
            Font = new Font("Segoe UI", 25, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 39, 83)
        };

        var subtitle = new Label
        {
            Text = "Monitoramento e Notificações",
            AutoSize = true,
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(3, 4, 3, 3)
        };

        var version = new Label
        {
            Text = $"Versão {ApplicationMetadata.Version}",
            AutoSize = true,
            ForeColor = Color.DimGray
        };

        var developer = new Label
        {
            Text = $"Desenvolvido por\n{ApplicationMetadata.DeveloperCompany}",
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            ForeColor = Color.FromArgb(70, 70, 70)
        };

        _status.AutoSize = true;
        _status.Text = "Preparando a aplicação...";
        _status.ForeColor = Color.FromArgb(55, 55, 55);
        _status.Margin = new Padding(3, 0, 3, 8);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 8;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 24;

        content.Controls.Add(title, 0, 0);
        content.Controls.Add(subtitle, 0, 1);
        content.Controls.Add(version, 0, 3);
        content.Controls.Add(developer, 0, 4);
        content.Controls.Add(_status, 0, 5);
        content.Controls.Add(_progress, 0, 6);

        root.Controls.Add(logoPanel, 0, 0);
        root.Controls.Add(content, 1, 0);
        Controls.Add(root);

        Paint += (_, eventArgs) =>
        {
            using var pen = new Pen(Color.FromArgb(205, 210, 218));
            eventArgs.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        };
    }

    internal void UpdateStatus(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        _status.Text = message;
        _status.Refresh();
        System.Windows.Forms.Application.DoEvents();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _developerLogo?.Dispose();
        }

        base.Dispose(disposing);
    }
}
