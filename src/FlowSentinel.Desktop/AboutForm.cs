namespace FlowSentinel.Desktop;

internal sealed class AboutForm : Form
{
    private readonly Image? _developerLogo;

    internal AboutForm()
    {
        _developerLogo = DesktopAssets.LoadDeveloperLogo();

        Text = "Sobre o FlowSentinel";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(620, 440);
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? SystemIcons.Application;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(24)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var logo = new PictureBox
        {
            Dock = DockStyle.Top,
            Height = 190,
            Image = _developerLogo,
            SizeMode = PictureBoxSizeMode.Zoom
        };

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
        left.Controls.Add(logo);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 12,
            Padding = new Padding(22, 8, 0, 0)
        };
        for (var index = 0; index < 12; index++)
        {
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var title = new Label
        {
            Text = "FlowSentinel",
            AutoSize = true,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(5, 39, 83)
        };
        var description = new Label
        {
            Text = "Plataforma local de monitoramento, regras, tarefas e notificações.",
            AutoSize = true,
            MaximumSize = new Size(360, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 6, 3, 12)
        };
        var version = new Label
        {
            Text = $"Versão {ApplicationMetadata.Version}",
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(3, 0, 3, 18)
        };
        var developedBy = new Label
        {
            Text = "Desenvolvido por",
            AutoSize = true,
            ForeColor = Color.DimGray
        };
        var company = new Label
        {
            Text = ApplicationMetadata.DeveloperCompany,
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(3, 2, 3, 2)
        };
        var developer = new Label
        {
            Text = ApplicationMetadata.DeveloperName,
            AutoSize = true,
            Margin = new Padding(3, 0, 3, 14)
        };

        var github = CreateLink($"GitHub: {ApplicationMetadata.GitHubUser}", ApplicationMetadata.GitHubUrl);
        var whatsapp = CreateLink($"WhatsApp: {ApplicationMetadata.WhatsAppDisplay}", ApplicationMetadata.WhatsAppUrl);
        var email = CreateLink($"E-mail: {ApplicationMetadata.Email}", $"mailto:{ApplicationMetadata.Email}");

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 20, 0, 0)
        };
        var copy = new Button { Text = "Copiar contatos", AutoSize = true, Height = 32 };
        copy.Click += (_, _) => CopyContacts();
        var close = new Button { Text = "Fechar", AutoSize = true, Height = 32, DialogResult = DialogResult.OK };
        buttons.Controls.Add(copy);
        buttons.Controls.Add(close);

        content.Controls.Add(title);
        content.Controls.Add(description);
        content.Controls.Add(version);
        content.Controls.Add(developedBy);
        content.Controls.Add(company);
        content.Controls.Add(developer);
        content.Controls.Add(github);
        content.Controls.Add(whatsapp);
        content.Controls.Add(email);
        content.Controls.Add(buttons);

        root.Controls.Add(left, 0, 0);
        root.Controls.Add(content, 1, 0);
        Controls.Add(root);
        AcceptButton = close;
        CancelButton = close;
    }

    private static LinkLabel CreateLink(string text, string target)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            LinkColor = Color.FromArgb(5, 75, 145),
            Margin = new Padding(3, 4, 3, 4)
        };
        link.LinkClicked += (_, _) => OpenTarget(target);
        return link;
    }

    private static void OpenTarget(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "FlowSentinel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void CopyContacts()
    {
        var contacts = $"{ApplicationMetadata.DeveloperCompany}{Environment.NewLine}" +
                       $"{ApplicationMetadata.DeveloperName}{Environment.NewLine}" +
                       $"GitHub: {ApplicationMetadata.GitHubUser}{Environment.NewLine}" +
                       $"WhatsApp: {ApplicationMetadata.WhatsAppDisplay}{Environment.NewLine}" +
                       $"E-mail: {ApplicationMetadata.Email}";
        Clipboard.SetText(contacts);
        MessageBox.Show(
            "Contatos copiados para a área de transferência.",
            "FlowSentinel",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
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
