namespace FlowSentinel.Desktop;

internal sealed class SplashForm : Form
{
    private readonly Label _status = new();
    private readonly Label _detail = new();
    private readonly Label _elapsed = new();
    private readonly ProgressBar _progress = new();
    private readonly Image? _developerLogo;

    internal SplashForm()
    {
        _developerLogo = DesktopAssets.LoadDeveloperLogoForDarkBackground();

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.White;
        ClientSize = new Size(700, 390);
        UseWaitCursor = true;
        DoubleBuffered = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(28)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logoPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(5, 39, 83),
            Padding = new Padding(32)
        };
        logoPanel.Controls.Add(new PictureBox
        {
            Dock = DockStyle.Fill,
            Image = _developerLogo,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        });

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 10,
            Padding = new Padding(32, 26, 12, 18)
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(new Label
        {
            Text = "FlowSentinel",
            AutoSize = true,
            Font = new Font("Segoe UI", 25, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 39, 83)
        }, 0, 0);
        content.Controls.Add(new Label
        {
            Text = "Monitoramento e Notificações",
            AutoSize = true,
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(3, 4, 3, 3)
        }, 0, 1);
        content.Controls.Add(new Label
        {
            Text = $"Versão {ApplicationMetadata.Version}",
            AutoSize = true,
            ForeColor = Color.DimGray
        }, 0, 3);
        content.Controls.Add(new Label
        {
            Text = $"Desenvolvido por\n{ApplicationMetadata.DeveloperCompany}",
            AutoSize = true,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(70, 70, 70)
        }, 0, 4);

        _status.AutoSize = true;
        _status.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _status.Text = "Preparando a aplicação...";
        _status.ForeColor = Color.FromArgb(45, 60, 80);
        _status.Margin = new Padding(3, 0, 3, 4);

        _detail.AutoSize = true;
        _detail.MaximumSize = new Size(360, 0);
        _detail.Text = "Inicializando componentes locais.";
        _detail.ForeColor = Color.DimGray;
        _detail.Margin = new Padding(3, 0, 3, 6);

        _elapsed.AutoSize = true;
        _elapsed.Text = "Tempo decorrido: 0 s";
        _elapsed.ForeColor = Color.Gray;
        _elapsed.Font = new Font("Segoe UI", 8.25F);
        _elapsed.Margin = new Padding(3, 0, 3, 8);

        _progress.Dock = DockStyle.Top;
        _progress.Height = 10;
        _progress.Style = ProgressBarStyle.Continuous;
        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Value = 0;

        content.Controls.Add(_status, 0, 5);
        content.Controls.Add(_detail, 0, 6);
        content.Controls.Add(_elapsed, 0, 7);
        content.Controls.Add(_progress, 0, 9);

        root.Controls.Add(logoPanel, 0, 0);
        root.Controls.Add(content, 1, 0);
        Controls.Add(root);

        Paint += (_, eventArgs) =>
        {
            using var pen = new Pen(Color.FromArgb(205, 210, 218));
            eventArgs.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        };
    }

    internal void UpdateStatus(string message, int progress, string? detail = null)
    {
        if (IsDisposed)
        {
            return;
        }

        _status.Text = message;
        _detail.Text = string.IsNullOrWhiteSpace(detail) ? message : detail;
        _elapsed.Text = "Tempo decorrido: 0 s";
        _elapsed.ForeColor = Color.Gray;
        _progress.Value = Math.Clamp(progress, _progress.Minimum, _progress.Maximum);
        Refresh();
    }

    internal void UpdateElapsed(string? stepName, TimeSpan elapsed, TimeSpan timeout)
    {
        if (IsDisposed)
        {
            return;
        }

        var seconds = Math.Max(0, (int)Math.Floor(elapsed.TotalSeconds));
        if (elapsed >= TimeSpan.FromSeconds(15))
        {
            _elapsed.Text = $"Inicialização acompanhada: {seconds} s de {timeout.TotalSeconds:N0} s permitidos.";
            _elapsed.ForeColor = Color.DarkGoldenrod;
        }
        else
        {
            _elapsed.Text = $"Tempo decorrido: {seconds} s";
            _elapsed.ForeColor = Color.Gray;
        }

        if (!string.IsNullOrWhiteSpace(stepName) && elapsed >= TimeSpan.FromSeconds(30))
        {
            _status.Text = $"{stepName} — ainda em processamento";
        }

        _elapsed.Refresh();
        _status.Refresh();
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
