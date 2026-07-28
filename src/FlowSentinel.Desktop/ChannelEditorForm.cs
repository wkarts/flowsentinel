using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class ChannelEditorForm : Form
{
    private readonly ISecretProtector _secretProtector;
    private readonly Guid _id;
    private readonly TextBox _name = new();
    private readonly ComboBox _type = new();
    private readonly CheckBox _enabled = new();
    private readonly ComboBox _scope = new();
    private readonly Panel _settingsHost = new();
    private readonly TextBox _advancedJson = new();

    private readonly TextBox _telegramToken = new();
    private readonly ComboBox _telegramParseMode = new();
    private readonly CheckBox _telegramSilent = new();

    private readonly TextBox _evolutionUrl = new();
    private readonly TextBox _evolutionApiKey = new();
    private readonly TextBox _evolutionInstance = new();
    private readonly ComboBox _evolutionVersion = new();
    private readonly TextBox _evolutionHeader = new();
    private readonly TextBox _evolutionSendPath = new();
    private readonly TextBox _evolutionStatePath = new();
    private readonly TextBox _evolutionConnectPath = new();

    private readonly ComboBox _emailPreset = new();
    private readonly TextBox _smtpHost = new();
    private readonly NumericUpDown _smtpPort = new();
    private readonly ComboBox _smtpSecurity = new();
    private readonly TextBox _smtpUsername = new();
    private readonly TextBox _smtpPassword = new();
    private readonly TextBox _fromAddress = new();
    private readonly TextBox _fromName = new();
    private readonly CheckBox _isHtml = new();
    private readonly NumericUpDown _timeout = new();

    internal ChannelConfiguration? Configuration { get; private set; }

    internal ChannelEditorForm(ChannelConfiguration configuration, ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
        _id = configuration.Id;
        Text = "Canal de notificação";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(860, 690);
        MinimumSize = new Size(720, 560);

        ConfigureControls();
        BuildLayout();
        LoadConfiguration(configuration);
    }

    private void ConfigureControls()
    {
        _type.DropDownStyle = ComboBoxStyle.DropDownList;
        _type.DataSource = Enum.GetValues<ChannelType>()
            .Select(x => new DisplayItem<ChannelType>(x, VisualEditorSupport.ChannelTypeText(x)))
            .ToList();
        _type.SelectedIndexChanged += (_, _) => RebuildSettings();
        _enabled.Text = "Canal habilitado";
        _enabled.AutoSize = true;
        _scope.DropDownStyle = ComboBoxStyle.DropDownList;
        _scope.Items.AddRange(["Usuário atual", "Máquina (compatível com serviço Windows)"]);
        _scope.SelectedIndex = 0;

        _timeout.Minimum = 5;
        _timeout.Maximum = 3600;
        _timeout.Value = 30;

        _telegramToken.UseSystemPasswordChar = true;
        _telegramParseMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _telegramParseMode.Items.AddRange(["HTML", "MarkdownV2", "Sem formatação"]);
        _telegramParseMode.SelectedIndex = 0;
        _telegramSilent.Text = "Enviar silenciosamente";
        _telegramSilent.AutoSize = true;

        _evolutionApiKey.UseSystemPasswordChar = true;
        _evolutionVersion.DropDownStyle = ComboBoxStyle.DropDownList;
        _evolutionVersion.Items.AddRange(["V2", "V1"]);
        _evolutionVersion.SelectedIndex = 0;
        _evolutionVersion.SelectedIndexChanged += (_, _) => ApplyEvolutionDefaults(overwrite: false);
        _evolutionHeader.Text = "apikey";

        _emailPreset.DropDownStyle = ComboBoxStyle.DropDownList;
        _emailPreset.Items.AddRange(["SMTP personalizado", "Gmail", "Outlook / Hotmail", "Microsoft 365"]);
        _emailPreset.SelectedIndex = 0;
        _emailPreset.SelectedIndexChanged += (_, _) => ApplyEmailPreset();
        _smtpPort.Minimum = 1;
        _smtpPort.Maximum = 65535;
        _smtpPort.Value = 587;
        _smtpSecurity.DropDownStyle = ComboBoxStyle.DropDownList;
        _smtpSecurity.Items.AddRange(["StartTls", "SslOnConnect", "None"]);
        _smtpSecurity.SelectedIndex = 0;
        _smtpPassword.UseSystemPasswordChar = true;
        _isHtml.Text = "Mensagem em HTML";
        _isHtml.AutoSize = true;

        _advancedJson.Multiline = true;
        _advancedJson.AcceptsTab = true;
        _advancedJson.ScrollBars = ScrollBars.Both;
        _advancedJson.WordWrap = false;
        _advancedJson.Font = new Font("Consolas", 10);
        _advancedJson.Dock = DockStyle.Fill;
    }

    private void BuildLayout()
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(8) };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddHeaderRow(header, 0, "Nome", _name, "Tipo", _type);
        AddHeaderRow(header, 1, "Proteção", _scope, "Situação", _enabled);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var visualTab = new TabPage("Configuração visual");
        _settingsHost.Dock = DockStyle.Fill;
        _settingsHost.AutoScroll = true;
        visualTab.Controls.Add(_settingsHost);
        var advancedTab = new TabPage("JSON avançado");
        var tools = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
        var update = new Button { Text = "Atualizar JSON pela tela", AutoSize = true };
        var apply = new Button { Text = "Aplicar JSON à tela", AutoSize = true };
        update.Click += (_, _) => UpdateAdvancedJson();
        apply.Click += (_, _) => ApplyAdvancedJson();
        tools.Controls.Add(update);
        tools.Controls.Add(apply);
        advancedTab.Controls.Add(_advancedJson);
        advancedTab.Controls.Add(tools);
        tabs.TabPages.Add(visualTab);
        tabs.TabPages.Add(advancedTab);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
        var save = new Button { Text = "Salvar canal", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        main.Controls.Add(header, 0, 0);
        main.Controls.Add(tabs, 0, 1);
        main.Controls.Add(buttons, 0, 2);
        Controls.Add(main);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static void AddHeaderRow(TableLayoutPanel panel, int row, string label1, Control control1, string label2, Control control2)
    {
        panel.Controls.Add(VisualEditorSupport.LabelFor(label1), 0, row);
        control1.Dock = DockStyle.Fill;
        panel.Controls.Add(control1, 1, row);
        panel.Controls.Add(VisualEditorSupport.LabelFor(label2), 2, row);
        control2.Dock = DockStyle.Fill;
        panel.Controls.Add(control2, 3, row);
    }

    private void RebuildSettings()
    {
        if (_type.SelectedItem is not DisplayItem<ChannelType> item) return;
        _settingsHost.Controls.Clear();
        var editor = item.Value switch
        {
            ChannelType.LocalWindows => BuildLocalPanel(),
            ChannelType.Telegram => BuildTelegramPanel(),
            ChannelType.EvolutionApi => BuildEvolutionPanel(),
            ChannelType.Email => BuildEmailPanel(),
            _ => new Label { Text = "Canal não suportado.", AutoSize = true }
        };
        editor.Dock = DockStyle.Top;
        _settingsHost.Controls.Add(editor);
    }

    private static Control BuildLocalPanel() => new Label
    {
        Text = "As notificações serão exibidas localmente no Windows. Nenhuma credencial é necessária.",
        AutoSize = true,
        MaximumSize = new Size(760, 0),
        Padding = new Padding(18)
    };

    private Control BuildTelegramPanel()
    {
        var table = CreateTable();
        AddRow(table, "Token do bot", _telegramToken);
        AddRow(table, "Formatação", _telegramParseMode);
        AddRow(table, "Notificação", _telegramSilent);
        AddRow(table, "Timeout (segundos)", _timeout);
        table.Controls.Add(Help("Crie o bot pelo BotFather. Os Chat IDs são cadastrados como destinatários nas ações."), 1, table.RowCount);
        return table;
    }

    private Control BuildEvolutionPanel()
    {
        var table = CreateTable();
        AddRow(table, "URL da Evolution API", _evolutionUrl);
        AddRow(table, "Versão", _evolutionVersion);
        AddRow(table, "API Key", _evolutionApiKey);
        AddRow(table, "Cabeçalho da API Key", _evolutionHeader);
        AddRow(table, "Instância", _evolutionInstance);
        AddRow(table, "Rota de envio", _evolutionSendPath);
        AddRow(table, "Rota de status", _evolutionStatePath);
        AddRow(table, "Rota de conexão/QR", _evolutionConnectPath);
        AddRow(table, "Timeout (segundos)", _timeout);
        var defaults = new Button { Text = "Restaurar rotas padrão da versão", AutoSize = true };
        defaults.Click += (_, _) => ApplyEvolutionDefaults(overwrite: true);
        AddRow(table, "Padrões", defaults);
        table.Controls.Add(Help("Depois de salvar, use os botões Status Evolution e QR Code Evolution na lista de canais."), 1, table.RowCount);
        return table;
    }

    private Control BuildEmailPanel()
    {
        var table = CreateTable();
        AddRow(table, "Provedor", _emailPreset);
        AddRow(table, "Servidor SMTP", _smtpHost);
        AddRow(table, "Porta", _smtpPort);
        AddRow(table, "Segurança", _smtpSecurity);
        AddRow(table, "Usuário", _smtpUsername);
        AddRow(table, "Senha / senha de aplicativo", _smtpPassword);
        AddRow(table, "E-mail remetente", _fromAddress);
        AddRow(table, "Nome do remetente", _fromName);
        AddRow(table, "Formato", _isHtml);
        AddRow(table, "Timeout (segundos)", _timeout);
        table.Controls.Add(Help("Para Gmail e contas Microsoft com autenticação em duas etapas, utilize senha de aplicativo ou uma conta SMTP autorizada."), 1, table.RowCount);
        return table;
    }

    private static TableLayoutPanel CreateTable()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static Label Help(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MaximumSize = new Size(650, 0),
        Padding = new Padding(0, 10, 0, 0)
    };

    private void Save()
    {
        try
        {
            Configuration = BuildConfiguration(protectSecrets: true);
            if (string.IsNullOrWhiteSpace(Configuration.Name)) throw new InvalidOperationException("Informe o nome do canal.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação do canal");
        }
    }

    private ChannelConfiguration BuildConfiguration(bool protectSecrets)
    {
        var type = SelectedType();
        object settings = type switch
        {
            ChannelType.LocalWindows => new { },
            ChannelType.Telegram => new
            {
                botToken = ProtectIfNeeded(Required(_telegramToken.Text, "Informe o token do bot Telegram."), protectSecrets),
                parseMode = _telegramParseMode.SelectedIndex == 2 ? null : Convert.ToString(_telegramParseMode.SelectedItem),
                disableNotification = _telegramSilent.Checked,
                timeoutSeconds = (int)_timeout.Value
            },
            ChannelType.EvolutionApi => new
            {
                baseUrl = Required(_evolutionUrl.Text, "Informe a URL da Evolution API.").TrimEnd('/'),
                apiKey = ProtectIfNeeded(Required(_evolutionApiKey.Text, "Informe a API Key da Evolution API."), protectSecrets),
                apiKeyHeader = string.IsNullOrWhiteSpace(_evolutionHeader.Text) ? "apikey" : _evolutionHeader.Text.Trim(),
                instance = Required(_evolutionInstance.Text, "Informe o nome da instância."),
                apiVersion = Convert.ToString(_evolutionVersion.SelectedItem) ?? "V2",
                sendTextPathTemplate = Required(_evolutionSendPath.Text, "Informe a rota de envio."),
                connectionStatePathTemplate = Required(_evolutionStatePath.Text, "Informe a rota de status."),
                connectPathTemplate = Required(_evolutionConnectPath.Text, "Informe a rota de conexão."),
                payloadMode = Convert.ToString(_evolutionVersion.SelectedItem) ?? "V2",
                timeoutSeconds = (int)_timeout.Value
            },
            ChannelType.Email => new
            {
                host = Required(_smtpHost.Text, "Informe o servidor SMTP."),
                port = (int)_smtpPort.Value,
                security = Convert.ToString(_smtpSecurity.SelectedItem) ?? "StartTls",
                username = _smtpUsername.Text.Trim(),
                password = ProtectIfNeeded(_smtpPassword.Text, protectSecrets),
                fromAddress = Required(_fromAddress.Text, "Informe o e-mail remetente."),
                fromName = string.IsNullOrWhiteSpace(_fromName.Text) ? "FlowSentinel" : _fromName.Text.Trim(),
                isHtml = _isHtml.Checked,
                timeoutSeconds = (int)_timeout.Value
            },
            _ => throw new ArgumentOutOfRangeException()
        };

        return new ChannelConfiguration
        {
            Id = _id == Guid.Empty ? Guid.NewGuid() : _id,
            Name = _name.Text.Trim(),
            Type = type,
            Enabled = _enabled.Checked,
            SettingsJson = JsonSerializer.Serialize(settings, FlowJson.Options)
        };
    }

    private string ProtectIfNeeded(string value, bool protect)
    {
        if (!protect || string.IsNullOrEmpty(value) || value.StartsWith("dpapi", StringComparison.OrdinalIgnoreCase)) return value;
        return _secretProtector.Protect(value,
            _scope.SelectedIndex == 1 ? SecretProtectionScope.LocalMachine : SecretProtectionScope.CurrentUser);
    }

    private void LoadConfiguration(ChannelConfiguration configuration)
    {
        _name.Text = configuration.Name;
        _enabled.Checked = configuration.Enabled;
        SelectType(configuration.Type);
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(configuration.SettingsJson) ? "{}" : configuration.SettingsJson);
            LoadSettings(configuration.Type, document.RootElement);
        }
        catch
        {
            _advancedJson.Text = configuration.SettingsJson;
        }
        RebuildSettings();
        UpdateAdvancedJson();
    }

    private void LoadSettings(ChannelType type, JsonElement settings)
    {
        string GetString(string name, string fallback = "") => settings.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
        bool GetBool(string name, bool fallback) => settings.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
        int GetInt(string name, int fallback) => settings.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;
        string Unprotect(string value)
        {
            try { return _secretProtector.UnprotectIfNeeded(value); } catch { return value; }
        }

        _timeout.Value = Math.Clamp(GetInt("timeoutSeconds", 30), 5, 3600);
        switch (type)
        {
            case ChannelType.Telegram:
                _telegramToken.Text = Unprotect(GetString("botToken"));
                _telegramParseMode.SelectedItem = GetString("parseMode", "HTML") switch
                {
                    "MarkdownV2" => "MarkdownV2",
                    "" => "Sem formatação",
                    _ => "HTML"
                };
                _telegramSilent.Checked = GetBool("disableNotification", false);
                break;
            case ChannelType.EvolutionApi:
                _evolutionUrl.Text = GetString("baseUrl");
                _evolutionApiKey.Text = Unprotect(GetString("apiKey"));
                _evolutionHeader.Text = GetString("apiKeyHeader", "apikey");
                _evolutionInstance.Text = GetString("instance");
                _evolutionVersion.SelectedItem = GetString("apiVersion", "V2").ToUpperInvariant() == "V1" ? "V1" : "V2";
                _evolutionSendPath.Text = GetString("sendTextPathTemplate", "/message/sendText/{instance}");
                _evolutionStatePath.Text = GetString("connectionStatePathTemplate", "/instance/connectionState/{instance}");
                _evolutionConnectPath.Text = GetString("connectPathTemplate", "/instance/connect/{instance}");
                break;
            case ChannelType.Email:
                _smtpHost.Text = GetString("host");
                _smtpPort.Value = Math.Clamp(GetInt("port", 587), 1, 65535);
                _smtpSecurity.SelectedItem = GetString("security", "StartTls");
                _smtpUsername.Text = GetString("username");
                _smtpPassword.Text = Unprotect(GetString("password"));
                _fromAddress.Text = GetString("fromAddress");
                _fromName.Text = GetString("fromName", "FlowSentinel");
                _isHtml.Checked = GetBool("isHtml", false);
                DetectEmailPreset();
                break;
        }
    }

    private void ApplyEvolutionDefaults(bool overwrite)
    {
        var version = Convert.ToString(_evolutionVersion.SelectedItem) ?? "V2";
        if (overwrite || string.IsNullOrWhiteSpace(_evolutionSendPath.Text)) _evolutionSendPath.Text = version == "V1" ? "/message/sendText/{instance}" : "/message/sendText/{instance}";
        if (overwrite || string.IsNullOrWhiteSpace(_evolutionStatePath.Text)) _evolutionStatePath.Text = "/instance/connectionState/{instance}";
        if (overwrite || string.IsNullOrWhiteSpace(_evolutionConnectPath.Text)) _evolutionConnectPath.Text = "/instance/connect/{instance}";
    }

    private void ApplyEmailPreset()
    {
        switch (Convert.ToString(_emailPreset.SelectedItem))
        {
            case "Gmail":
                _smtpHost.Text = "smtp.gmail.com";
                _smtpPort.Value = 587;
                _smtpSecurity.SelectedItem = "StartTls";
                break;
            case "Outlook / Hotmail":
                _smtpHost.Text = "smtp-mail.outlook.com";
                _smtpPort.Value = 587;
                _smtpSecurity.SelectedItem = "StartTls";
                break;
            case "Microsoft 365":
                _smtpHost.Text = "smtp.office365.com";
                _smtpPort.Value = 587;
                _smtpSecurity.SelectedItem = "StartTls";
                break;
        }
    }

    private void DetectEmailPreset()
    {
        _emailPreset.SelectedItem = _smtpHost.Text.ToLowerInvariant() switch
        {
            "smtp.gmail.com" => "Gmail",
            "smtp-mail.outlook.com" => "Outlook / Hotmail",
            "smtp.office365.com" => "Microsoft 365",
            _ => "SMTP personalizado"
        };
    }

    private void UpdateAdvancedJson()
    {
        try
        {
            _advancedJson.Text = BuildConfiguration(protectSecrets: false).SettingsJson;
        }
        catch (Exception exception)
        {
            _advancedJson.Text = $"Não foi possível gerar o JSON: {exception.Message}";
        }
    }

    private void ApplyAdvancedJson()
    {
        try
        {
            using var document = JsonDocument.Parse(_advancedJson.Text);
            LoadSettings(SelectedType(), document.RootElement);
            RebuildSettings();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "JSON do canal");
        }
    }

    private ChannelType SelectedType() => (_type.SelectedItem as DisplayItem<ChannelType>)?.Value ?? ChannelType.LocalWindows;
    private void SelectType(ChannelType value) => _type.SelectedItem = _type.Items.Cast<DisplayItem<ChannelType>>().First(x => x.Value == value);

    private static string Required(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(message);
        return value.Trim();
    }
}
