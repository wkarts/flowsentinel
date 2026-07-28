using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ChannelEditorForm : Form
{
    private readonly ISecretProtector _secretProtector;
    private readonly TextBox _name = new();
    private readonly ComboBox _type = new();
    private readonly CheckBox _enabled = new();
    private readonly TextBox _settings = new();
    private readonly Guid _id;

    internal ChannelConfiguration? Configuration { get; private set; }

    public ChannelEditorForm(ChannelConfiguration configuration, ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
        _id = configuration.Id;
        Text = "Configuração do canal";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(800, 650);
        MinimumSize = new Size(650, 480);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(8)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _name.Dock = DockStyle.Fill;
        _name.Text = configuration.Name;
        _type.Dock = DockStyle.Fill;
        _type.DropDownStyle = ComboBoxStyle.DropDownList;
        _type.DataSource = Enum.GetValues<ChannelType>();
        _type.SelectedItem = configuration.Type;
        _enabled.Text = "Canal habilitado";
        _enabled.Checked = configuration.Enabled;
        _enabled.AutoSize = true;

        header.Controls.Add(new Label { Text = "Nome", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        header.Controls.Add(_name, 1, 0);
        header.Controls.Add(new Label { Text = "Tipo", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        header.Controls.Add(_type, 1, 1);
        header.Controls.Add(new Label { Text = "Situação", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        header.Controls.Add(_enabled, 1, 2);

        _settings.Multiline = true;
        _settings.AcceptsTab = true;
        _settings.ScrollBars = ScrollBars.Both;
        _settings.WordWrap = false;
        _settings.Font = new Font("Consolas", 10);
        _settings.Dock = DockStyle.Fill;
        _settings.Text = configuration.SettingsJson;

        var hint = new Label
        {
            Text = "Selecione no JSON somente o segredo. Use proteção por usuário para o Desktop ou por máquina para compartilhamento com o serviço.",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8, 4, 8, 4)
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8)
        };
        var save = new Button { Text = "Salvar", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        var format = new Button { Text = "Formatar JSON", AutoSize = true };
        var protectUser = new Button { Text = "Proteger para usuário", AutoSize = true };
        var protectMachine = new Button { Text = "Proteger para máquina", AutoSize = true };
        var template = new Button { Text = "Modelo do tipo", AutoSize = true };
        save.Click += (_, _) => Save();
        format.Click += (_, _) => FormatJson();
        protectUser.Click += (_, _) => ProtectSelection(SecretProtectionScope.CurrentUser);
        protectMachine.Click += (_, _) => ProtectSelection(SecretProtectionScope.LocalMachine);
        template.Click += (_, _) => ApplyTemplate();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(protectMachine);
        buttons.Controls.Add(protectUser);
        buttons.Controls.Add(format);
        buttons.Controls.Add(template);

        Controls.Add(_settings);
        Controls.Add(hint);
        Controls.Add(header);
        Controls.Add(buttons);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private void Save()
    {
        try
        {
            using var document = JsonDocument.Parse(_settings.Text);
            Configuration = new ChannelConfiguration
            {
                Id = _id,
                Name = _name.Text.Trim(),
                Type = (ChannelType)_type.SelectedItem!,
                Enabled = _enabled.Checked,
                SettingsJson = JsonSerializer.Serialize(document.RootElement, FlowJson.Options)
            };
            if (string.IsNullOrWhiteSpace(Configuration.Name))
            {
                throw new InvalidOperationException("Informe o nome do canal.");
            }
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Validação", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void FormatJson()
    {
        try
        {
            using var document = JsonDocument.Parse(_settings.Text);
            _settings.Text = JsonSerializer.Serialize(document.RootElement, FlowJson.Options);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "JSON", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ProtectSelection(SecretProtectionScope scope)
    {
        if (string.IsNullOrEmpty(_settings.SelectedText))
        {
            MessageBox.Show(this, "Selecione apenas o conteúdo do segredo dentro das aspas.", "DPAPI", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try
        {
            _settings.SelectedText = _secretProtector.Protect(_settings.SelectedText, scope);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "DPAPI", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyTemplate()
    {
        _settings.Text = ((ChannelType)_type.SelectedItem!) switch
        {
            ChannelType.LocalWindows => "{}",
            ChannelType.Telegram => """
            {
              "botToken": "TOKEN_DO_BOT",
              "parseMode": "HTML",
              "disableNotification": false,
              "timeoutSeconds": 30
            }
            """,
            ChannelType.EvolutionApi => """
            {
              "baseUrl": "https://evolution.exemplo.com",
              "apiKey": "API_KEY",
              "apiKeyHeader": "apikey",
              "instance": "empresa-principal",
              "apiVersion": "V2",
              "sendTextPathTemplate": "/message/sendText/{instance}",
              "connectionStatePathTemplate": "/instance/connectionState/{instance}",
              "connectPathTemplate": "/instance/connect/{instance}",
              "payloadMode": "V2",
              "timeoutSeconds": 30
            }
            """,
            ChannelType.Email => """
            {
              "host": "smtp.exemplo.com",
              "port": 587,
              "security": "StartTls",
              "username": "usuario@exemplo.com",
              "password": "SENHA",
              "fromAddress": "usuario@exemplo.com",
              "fromName": "FlowSentinel",
              "isHtml": false,
              "timeoutSeconds": 30
            }
            """,
            _ => "{}"
        };
        FormatJson();
    }
}
