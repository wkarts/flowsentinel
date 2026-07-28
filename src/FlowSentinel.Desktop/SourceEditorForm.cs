using System.Data;
using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class SourceEditorForm : Form
{
    private readonly ISourceDesignerService _designer;
    private readonly ISecretProtector _secretProtector;
    private readonly Guid _id;

    private readonly TextBox _name = new();
    private readonly TextBox _alias = new();
    private readonly ComboBox _type = new();
    private readonly CheckBox _primary = new();
    private readonly CheckBox _enabled = new();
    private readonly TextBox _keys = new();
    private readonly Panel _configurationHost = new();
    private readonly DataGridView _preview = new();
    private readonly Label _previewSummary = new();
    private readonly TextBox _advancedJson = new();

    private readonly TextBox _filePath = new();
    private readonly ComboBox _worksheet = new();
    private readonly NumericUpDown _headerRow = new();
    private readonly CheckBox _ignoreEmpty = new();

    private readonly ComboBox _delimiter = new();
    private readonly TextBox _quote = new();
    private readonly ComboBox _encoding = new();
    private readonly CheckBox _hasHeader = new();

    private readonly ComboBox _textMode = new();
    private readonly TextBox _keyValueSeparator = new();
    private readonly TextBox _recordRegex = new();

    private readonly ComboBox _provider = new();
    private readonly TextBox _server = new();
    private readonly NumericUpDown _port = new();
    private readonly TextBox _database = new();
    private readonly TextBox _user = new();
    private readonly TextBox _password = new();
    private readonly TextBox _databaseFile = new();
    private readonly TextBox _connectionString = new();
    private readonly TextBox _query = new();
    private readonly NumericUpDown _timeout = new();
    private readonly DataGridView _parameters = new();
    private readonly ComboBox _protectionScope = new();
    private readonly List<string> _previewColumns = [];

    internal DataSourceDefinition? Definition { get; private set; }

    internal SourceEditorForm(
        DataSourceDefinition definition,
        ISourceDesignerService designer,
        ISecretProtector secretProtector)
    {
        _designer = designer;
        _secretProtector = secretProtector;
        _id = definition.Id;

        Text = "Fonte de dados";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1040, 760);
        MinimumSize = new Size(900, 650);

        ConfigureControls();
        BuildLayout();
        LoadDefinition(definition);
    }

    private void ConfigureControls()
    {
        _type.DropDownStyle = ComboBoxStyle.DropDownList;
        _type.DataSource = Enum.GetValues<SourceType>()
            .Select(x => new DisplayItem<SourceType>(x, VisualEditorSupport.SourceTypeText(x)))
            .ToList();
        _type.SelectedIndexChanged += (_, _) => RebuildConfigurationPanel();

        _primary.Text = "Fonte principal";
        _enabled.Text = "Fonte habilitada";
        _primary.AutoSize = _enabled.AutoSize = true;

        _filePath.Dock = DockStyle.Fill;
        _worksheet.DropDownStyle = ComboBoxStyle.DropDown;
        _headerRow.Minimum = 1;
        _headerRow.Maximum = 100000;
        _headerRow.Value = 1;
        _ignoreEmpty.Text = "Ignorar linhas vazias";
        _ignoreEmpty.Checked = true;
        _ignoreEmpty.AutoSize = true;

        _delimiter.DropDownStyle = ComboBoxStyle.DropDown;
        _delimiter.Items.AddRange([";", ",", "|", "\\t"]);
        _delimiter.Text = ";";
        _quote.Text = "\"";
        _encoding.DropDownStyle = ComboBoxStyle.DropDown;
        _encoding.Items.AddRange(["utf-8", "windows-1252", "iso-8859-1"]);
        _encoding.Text = "utf-8";
        _hasHeader.Text = "O arquivo possui cabeçalho";
        _hasHeader.Checked = true;
        _hasHeader.AutoSize = true;

        _textMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _textMode.Items.AddRange(["Uma ocorrência por linha", "Chave e valor"]);
        _textMode.SelectedIndex = 0;
        _textMode.SelectedIndexChanged += (_, _) => RebuildConfigurationPanel();
        _keyValueSeparator.Text = "=";

        _provider.DropDownStyle = ComboBoxStyle.DropDownList;
        _provider.DataSource = Enum.GetValues<DatabaseProvider>()
            .Select(x => new DisplayItem<DatabaseProvider>(x, x switch
            {
                DatabaseProvider.Sqlite => "SQLite",
                DatabaseProvider.SqlServer => "SQL Server",
                DatabaseProvider.MySql => "MySQL / MariaDB",
                DatabaseProvider.PostgreSql => "PostgreSQL",
                DatabaseProvider.Firebird => "Firebird",
                _ => x.ToString()
            })).ToList();
        _provider.SelectedIndexChanged += (_, _) => RebuildConfigurationPanel();
        _port.Minimum = 0;
        _port.Maximum = 65535;
        _port.Value = 0;
        _password.UseSystemPasswordChar = true;
        _connectionString.Multiline = true;
        _connectionString.Height = 62;
        _connectionString.ScrollBars = ScrollBars.Vertical;
        _query.Multiline = true;
        _query.AcceptsTab = true;
        _query.ScrollBars = ScrollBars.Both;
        _query.WordWrap = false;
        _query.Font = new Font("Consolas", 10);
        _query.Height = 145;
        _timeout.Minimum = 5;
        _timeout.Maximum = 3600;
        _timeout.Value = 30;
        _protectionScope.DropDownStyle = ComboBoxStyle.DropDownList;
        _protectionScope.Items.AddRange(["Usuário atual", "Máquina (serviço Windows)"]);
        _protectionScope.SelectedIndex = 0;

        _parameters.AllowUserToAddRows = true;
        _parameters.AllowUserToDeleteRows = true;
        _parameters.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _parameters.Height = 120;
        _parameters.Columns.Add("ParameterName", "Parâmetro");
        _parameters.Columns.Add("ParameterValue", "Valor");

        _preview.ReadOnly = true;
        _preview.AllowUserToAddRows = false;
        _preview.AllowUserToDeleteRows = false;
        _preview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _preview.Dock = DockStyle.Fill;
        _previewSummary.Dock = DockStyle.Top;
        _previewSummary.Height = 34;
        _previewSummary.Padding = new Padding(8);

        _advancedJson.Multiline = true;
        _advancedJson.AcceptsTab = true;
        _advancedJson.ScrollBars = ScrollBars.Both;
        _advancedJson.WordWrap = false;
        _advancedJson.Font = new Font("Consolas", 10);
        _advancedJson.Dock = DockStyle.Fill;
    }

    private void BuildLayout()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddRow(header, 0, "Nome", _name, "Alias", _alias);
        AddRow(header, 1, "Tipo", _type, "Campos-chave", _keys);
        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        flags.Controls.Add(_enabled);
        flags.Controls.Add(_primary);
        header.Controls.Add(VisualEditorSupport.LabelFor("Situação"), 0, 2);
        header.Controls.Add(flags, 1, 2);
        header.SetColumnSpan(flags, 3);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var configurationTab = new TabPage("Configuração");
        _configurationHost.Dock = DockStyle.Fill;
        _configurationHost.AutoScroll = true;
        configurationTab.Controls.Add(_configurationHost);

        var previewTab = new TabPage("Pré-visualização");
        var previewToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6) };
        var previewButton = new Button { Text = "Ler amostra", AutoSize = true };
        var testButton = new Button { Text = "Testar fonte", AutoSize = true };
        previewButton.Click += async (_, _) => await PreviewAsync();
        testButton.Click += async (_, _) => await TestAsync();
        previewToolbar.Controls.Add(previewButton);
        previewToolbar.Controls.Add(testButton);
        previewTab.Controls.Add(_preview);
        previewTab.Controls.Add(_previewSummary);
        previewTab.Controls.Add(previewToolbar);

        var advancedTab = new TabPage("JSON avançado");
        var advancedButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(6) };
        var updateJson = new Button { Text = "Atualizar JSON pela tela", AutoSize = true };
        var applyJson = new Button { Text = "Aplicar JSON à tela", AutoSize = true };
        updateJson.Click += (_, _) => UpdateAdvancedJson();
        applyJson.Click += (_, _) => ApplyAdvancedJson();
        advancedButtons.Controls.Add(updateJson);
        advancedButtons.Controls.Add(applyJson);
        advancedTab.Controls.Add(_advancedJson);
        advancedTab.Controls.Add(advancedButtons);

        tabs.TabPages.Add(configurationTab);
        tabs.TabPages.Add(previewTab);
        tabs.TabPages.Add(advancedTab);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };
        var save = new Button { Text = "Salvar fonte", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => Save();
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;

        main.Controls.Add(header, 0, 0);
        main.Controls.Add(tabs, 0, 1);
        main.Controls.Add(buttons, 0, 2);
        Controls.Add(main);
    }

    private static void AddRow(TableLayoutPanel panel, int row, string label1, Control control1, string label2, Control control2)
    {
        panel.Controls.Add(VisualEditorSupport.LabelFor(label1), 0, row);
        control1.Dock = DockStyle.Fill;
        panel.Controls.Add(control1, 1, row);
        panel.Controls.Add(VisualEditorSupport.LabelFor(label2), 2, row);
        control2.Dock = DockStyle.Fill;
        panel.Controls.Add(control2, 3, row);
    }

    private void RebuildConfigurationPanel()
    {
        if (_type.SelectedItem is not DisplayItem<SourceType> selected)
        {
            return;
        }

        _configurationHost.SuspendLayout();
        _configurationHost.Controls.Clear();
        Control editor = selected.Value switch
        {
            SourceType.Excel => BuildExcelPanel(),
            SourceType.Csv => BuildCsvPanel(),
            SourceType.Text => BuildTextPanel(),
            SourceType.Database => BuildDatabasePanel(),
            _ => new Label { Text = "Tipo de fonte não suportado.", AutoSize = true }
        };
        editor.Dock = DockStyle.Top;
        _configurationHost.Controls.Add(editor);
        _configurationHost.ResumeLayout();
    }

    private Control BuildExcelPanel()
    {
        var table = CreateEditorTable();
        var filePanel = CreateBrowsePanel(_filePath, "Planilhas Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Todos os arquivos (*.*)|*.*", async () => await LoadWorksheetsAsync());
        AddEditorRow(table, "Arquivo", filePanel);
        var sheets = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        _worksheet.Width = 280;
        var load = new Button { Text = "Listar abas", AutoSize = true };
        load.Click += async (_, _) => await LoadWorksheetsAsync();
        sheets.Controls.Add(_worksheet);
        sheets.Controls.Add(load);
        AddEditorRow(table, "Planilha/aba", sheets);
        AddEditorRow(table, "Linha do cabeçalho", _headerRow);
        AddEditorRow(table, "Leitura", _ignoreEmpty);
        return WrapWithHelp(table, "Selecione a planilha, escolha a aba e informe quais colunas identificam cada registro.");
    }

    private Control BuildCsvPanel()
    {
        var table = CreateEditorTable();
        AddEditorRow(table, "Arquivo", CreateBrowsePanel(_filePath, "Arquivos CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*"));
        AddEditorRow(table, "Delimitador", _delimiter);
        AddEditorRow(table, "Caractere de aspas", _quote);
        AddEditorRow(table, "Codificação", _encoding);
        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        flags.Controls.Add(_hasHeader);
        flags.Controls.Add(_ignoreEmpty);
        AddEditorRow(table, "Opções", flags);
        return WrapWithHelp(table, "Use 'Ler amostra' para conferir as colunas e depois informe os campos-chave no topo.");
    }

    private Control BuildTextPanel()
    {
        var table = CreateEditorTable();
        AddEditorRow(table, "Arquivo", CreateBrowsePanel(_filePath, "Arquivos TXT (*.txt;*.log)|*.txt;*.log|Todos os arquivos (*.*)|*.*"));
        AddEditorRow(table, "Modo de leitura", _textMode);
        AddEditorRow(table, "Codificação", _encoding);
        if (_textMode.SelectedIndex == 1)
        {
            AddEditorRow(table, "Separador chave/valor", _keyValueSeparator);
        }
        else
        {
            _recordRegex.Multiline = true;
            _recordRegex.Height = 80;
            AddEditorRow(table, "Expressão regular opcional", _recordRegex);
        }
        AddEditorRow(table, "Leitura", _ignoreEmpty);
        return WrapWithHelp(table, "No modo por linha, os campos padrão são LineNumber e Content. Grupos nomeados da regex viram campos.");
    }

    private Control BuildDatabasePanel()
    {
        var outer = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        var table = CreateEditorTable();
        AddEditorRow(table, "Provedor", _provider);
        var provider = SelectedProvider();
        if (provider == DatabaseProvider.Sqlite)
        {
            AddEditorRow(table, "Arquivo SQLite", CreateBrowsePanel(_databaseFile, "Bancos SQLite (*.db;*.sqlite;*.sqlite3)|*.db;*.sqlite;*.sqlite3|Todos os arquivos (*.*)|*.*"));
        }
        else
        {
            AddEditorRow(table, "Servidor", _server);
            AddEditorRow(table, "Porta", _port);
            AddEditorRow(table, provider == DatabaseProvider.Firebird ? "Banco/arquivo ou alias" : "Banco de dados", _database);
            AddEditorRow(table, "Usuário", _user);
            AddEditorRow(table, "Senha", _password);
        }
        AddEditorRow(table, "Proteção", _protectionScope);

        var build = new Button { Text = "Montar connection string", AutoSize = true };
        build.Click += (_, _) => _connectionString.Text = BuildConnectionString();
        AddEditorRow(table, "Conexão", build);
        AddEditorRow(table, "Connection string", _connectionString);
        AddEditorRow(table, "Timeout (segundos)", _timeout);
        AddEditorRow(table, "Consulta SELECT / WITH", _query);
        AddEditorRow(table, "Parâmetros", _parameters);

        outer.Controls.Add(new Label
        {
            Text = "A senha ou a connection string será protegida automaticamente ao salvar. Somente consultas de leitura são aceitas.",
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Padding = new Padding(4, 8, 4, 8)
        });
        outer.Controls.Add(table);
        return outer;
    }

    private static TableLayoutPanel CreateEditorTable()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return table;
    }

    private static void AddEditorRow(TableLayoutPanel table, string label, Control control)
    {
        var row = table.RowCount++;
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
    }

    private static Control WrapWithHelp(Control editor, string help)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        panel.Controls.Add(new Label { Text = help, AutoSize = true, MaximumSize = new Size(850, 0), Padding = new Padding(10) });
        panel.Controls.Add(editor);
        return panel;
    }

    private FlowLayoutPanel CreateBrowsePanel(TextBox textBox, string filter, Func<Task>? afterBrowse = null)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        textBox.Width = 570;
        var button = new Button { Text = "Procurar...", AutoSize = true };
        button.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                textBox.Text = dialog.FileName;
                if (afterBrowse is not null)
                {
                    await afterBrowse();
                }
            }
        };
        panel.Controls.Add(textBox);
        panel.Controls.Add(button);
        return panel;
    }

    private async Task LoadWorksheetsAsync()
    {
        try
        {
            UseWaitCursor = true;
            var sheets = await _designer.GetExcelWorksheetsAsync(_filePath.Text.Trim(), CancellationToken.None);
            var current = _worksheet.Text;
            _worksheet.Items.Clear();
            _worksheet.Items.AddRange(sheets.Cast<object>().ToArray());
            if (sheets.Count > 0)
            {
                _worksheet.SelectedItem = sheets.Contains(current, StringComparer.OrdinalIgnoreCase) ? current : sheets[0];
            }
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Planilha Excel");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task PreviewAsync()
    {
        try
        {
            UseWaitCursor = true;
            var source = BuildDefinition(requireKeys: false, protectSecrets: false);
            var result = await _designer.PreviewAsync(source, 100, CancellationToken.None);
            var table = new DataTable();
            foreach (var column in result.Columns)
            {
                table.Columns.Add(column);
            }
            foreach (var row in result.Rows)
            {
                var dataRow = table.NewRow();
                foreach (var column in result.Columns)
                {
                    dataRow[column] = row.TryGetValue(column, out var value) ? value ?? string.Empty : string.Empty;
                }
                table.Rows.Add(dataRow);
            }
            _previewColumns.Clear();
            _previewColumns.AddRange(result.Columns.Where(x => !x.StartsWith("__", StringComparison.Ordinal)));
            _preview.DataSource = table;
            _previewSummary.Text = $"{result.TotalRead} registro(s) lido(s); mostrando até 100. Tempo: {result.Duration.TotalMilliseconds:N0} ms. Colunas: {string.Join(", ", result.Columns)}";
            if (string.IsNullOrWhiteSpace(_keys.Text) && result.Columns.Count > 0)
            {
                var suggested = result.Columns.FirstOrDefault(x => !x.StartsWith("__", StringComparison.Ordinal)) ?? result.Columns[0];
                _keys.Text = suggested;
            }
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Pré-visualização");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task TestAsync()
    {
        try
        {
            UseWaitCursor = true;
            var result = await _designer.TestAsync(BuildDefinition(requireKeys: false, protectSecrets: false), CancellationToken.None);
            MessageBox.Show(this, $"{result.Message}\nTempo: {result.Duration.TotalMilliseconds:N0} ms", "Teste da fonte", MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Teste da fonte");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void Save()
    {
        try
        {
            Definition = BuildDefinition(requireKeys: true, protectSecrets: true);
            Definition.Validate();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Validação da fonte");
        }
    }

    private DataSourceDefinition BuildDefinition(bool requireKeys, bool protectSecrets)
    {
        var type = SelectedSourceType();
        var keys = _keys.Text.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (requireKeys && keys.Count == 0)
        {
            throw new InvalidOperationException("Informe ao menos um campo-chave para identificar cada registro.");
        }

        object settings = type switch
        {
            SourceType.Excel => new
            {
                filePath = Required(_filePath.Text, "Informe o arquivo Excel."),
                worksheet = string.IsNullOrWhiteSpace(_worksheet.Text) ? null : _worksheet.Text.Trim(),
                headerRow = (int)_headerRow.Value,
                ignoreEmptyRows = _ignoreEmpty.Checked,
                designerFields = _previewColumns
            },
            SourceType.Csv => new
            {
                filePath = Required(_filePath.Text, "Informe o arquivo CSV."),
                delimiter = _delimiter.Text == "\\t" ? "\t" : Required(_delimiter.Text, "Informe o delimitador."),
                quote = string.IsNullOrEmpty(_quote.Text) ? "\"" : _quote.Text[..1],
                encoding = string.IsNullOrWhiteSpace(_encoding.Text) ? "utf-8" : _encoding.Text.Trim(),
                hasHeader = _hasHeader.Checked,
                ignoreEmptyLines = _ignoreEmpty.Checked,
                designerFields = _previewColumns
            },
            SourceType.Text => new
            {
                filePath = Required(_filePath.Text, "Informe o arquivo TXT."),
                encoding = string.IsNullOrWhiteSpace(_encoding.Text) ? "utf-8" : _encoding.Text.Trim(),
                mode = _textMode.SelectedIndex == 1 ? "KeyValue" : "Lines",
                keyValueSeparator = string.IsNullOrEmpty(_keyValueSeparator.Text) ? "=" : _keyValueSeparator.Text,
                recordRegex = string.IsNullOrWhiteSpace(_recordRegex.Text) ? null : _recordRegex.Text,
                ignoreEmptyLines = _ignoreEmpty.Checked,
                designerFields = _previewColumns
            },
            SourceType.Database => BuildDatabaseSettings(protectSecrets),
            _ => throw new ArgumentOutOfRangeException()
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings, FlowJson.Options));
        return new DataSourceDefinition
        {
            Id = _id == Guid.Empty ? Guid.NewGuid() : _id,
            Name = Required(_name.Text, "Informe o nome da fonte."),
            Alias = Required(_alias.Text, "Informe o alias da fonte."),
            Type = type,
            IsPrimary = _primary.Checked,
            Enabled = _enabled.Checked,
            KeyFields = keys,
            Configuration = document.RootElement.Clone()
        };
    }

    private object BuildDatabaseSettings(bool protectSecrets)
    {
        var connectionString = string.IsNullOrWhiteSpace(_connectionString.Text)
            ? BuildConnectionString()
            : _connectionString.Text.Trim();
        if (protectSecrets && !connectionString.StartsWith("dpapi", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = _secretProtector.Protect(
                connectionString,
                _protectionScope.SelectedIndex == 1 ? SecretProtectionScope.LocalMachine : SecretProtectionScope.CurrentUser);
        }

        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (DataGridViewRow row in _parameters.Rows)
        {
            if (row.IsNewRow) continue;
            var name = Convert.ToString(row.Cells[0].Value)?.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                parameters[name] = Convert.ToString(row.Cells[1].Value);
            }
        }

        return new
        {
            provider = SelectedProvider(),
            connectionString,
            query = Required(_query.Text, "Informe a consulta SQL."),
            commandTimeoutSeconds = (int)_timeout.Value,
            parameters,
            designerFields = _previewColumns
        };
    }

    private string BuildConnectionString()
    {
        var provider = SelectedProvider();
        if (provider == DatabaseProvider.Sqlite)
        {
            return $"Data Source={Required(_databaseFile.Text, "Informe o arquivo SQLite.")};Pooling=True;Foreign Keys=True";
        }

        var server = Required(_server.Text, "Informe o servidor.");
        var database = Required(_database.Text, "Informe o banco de dados ou alias.");
        var user = Required(_user.Text, "Informe o usuário.");
        var password = _password.Text;
        var port = (int)_port.Value;

        return provider switch
        {
            DatabaseProvider.SqlServer => $"Server={server}{(port > 0 ? $",{port}" : string.Empty)};Database={database};User Id={user};Password={password};TrustServerCertificate=True;Encrypt=False",
            DatabaseProvider.MySql => $"Server={server};{(port > 0 ? $"Port={port};" : string.Empty)}Database={database};User ID={user};Password={password};",
            DatabaseProvider.PostgreSql => $"Host={server};{(port > 0 ? $"Port={port};" : string.Empty)}Database={database};Username={user};Password={password};",
            DatabaseProvider.Firebird => $"User={user};Password={password};Database={server}{(port > 0 ? $"/{port}" : string.Empty)}:{database};Charset=UTF8;Dialect=3;",
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void LoadDefinition(DataSourceDefinition definition)
    {
        _name.Text = definition.Name;
        _alias.Text = definition.Alias;
        _primary.Checked = definition.IsPrimary;
        _enabled.Checked = definition.Enabled;
        _keys.Text = string.Join(", ", definition.KeyFields);
        SelectSourceType(definition.Type);
        LoadConfiguration(definition.Type, definition.Configuration);
        UpdateAdvancedJson();
        RebuildConfigurationPanel();
    }

    private void LoadConfiguration(SourceType type, JsonElement configuration)
    {
        if (configuration.ValueKind != JsonValueKind.Object) return;
        string GetString(string name, string fallback = "") => configuration.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
        bool GetBool(string name, bool fallback) => configuration.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;
        int GetInt(string name, int fallback) => configuration.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;

        _filePath.Text = GetString("filePath");
        _encoding.Text = GetString("encoding", "utf-8");
        _ignoreEmpty.Checked = GetBool(type == SourceType.Excel ? "ignoreEmptyRows" : "ignoreEmptyLines", true);
        _previewColumns.Clear();
        if (configuration.TryGetProperty("designerFields", out var designerFields) && designerFields.ValueKind == JsonValueKind.Array)
        {
            _previewColumns.AddRange(designerFields.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!));
        }

        switch (type)
        {
            case SourceType.Excel:
                _worksheet.Text = GetString("worksheet");
                _headerRow.Value = Math.Clamp(GetInt("headerRow", 1), 1, 100000);
                break;
            case SourceType.Csv:
                _delimiter.Text = GetString("delimiter", ";") == "\t" ? "\\t" : GetString("delimiter", ";");
                _quote.Text = GetString("quote", "\"");
                _hasHeader.Checked = GetBool("hasHeader", true);
                break;
            case SourceType.Text:
                _textMode.SelectedIndex = GetString("mode", "Lines").Equals("KeyValue", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                _keyValueSeparator.Text = GetString("keyValueSeparator", "=");
                _recordRegex.Text = GetString("recordRegex");
                break;
            case SourceType.Database:
                SelectProvider(configuration.TryGetProperty("provider", out var providerElement)
                    ? ParseEnum(providerElement, DatabaseProvider.Sqlite)
                    : DatabaseProvider.Sqlite);
                var protectedConnection = GetString("connectionString");
                try { _connectionString.Text = _secretProtector.UnprotectIfNeeded(protectedConnection); }
                catch { _connectionString.Text = protectedConnection; }
                _query.Text = GetString("query");
                _timeout.Value = Math.Clamp(GetInt("commandTimeoutSeconds", 30), 5, 3600);
                if (configuration.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in parameters.EnumerateObject())
                    {
                        _parameters.Rows.Add(property.Name, property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.ToString());
                    }
                }
                break;
        }
    }

    private static T ParseEnum<T>(JsonElement element, T fallback) where T : struct, Enum
    {
        if (element.ValueKind == JsonValueKind.String && Enum.TryParse<T>(element.GetString(), true, out var result)) return result;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number) && Enum.IsDefined(typeof(T), number)) return (T)Enum.ToObject(typeof(T), number);
        return fallback;
    }

    private void UpdateAdvancedJson()
    {
        try
        {
            var source = BuildDefinition(requireKeys: false, protectSecrets: false);
            _advancedJson.Text = JsonSerializer.Serialize(source.Configuration, FlowJson.Options);
        }
        catch (Exception exception)
        {
            _advancedJson.Text = $"Não foi possível montar o JSON: {exception.Message}";
        }
    }

    private void ApplyAdvancedJson()
    {
        try
        {
            using var document = JsonDocument.Parse(_advancedJson.Text);
            LoadConfiguration(SelectedSourceType(), document.RootElement);
            RebuildConfigurationPanel();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "JSON da fonte");
        }
    }

    private SourceType SelectedSourceType() => (_type.SelectedItem as DisplayItem<SourceType>)?.Value ?? SourceType.Excel;
    private DatabaseProvider SelectedProvider() => (_provider.SelectedItem as DisplayItem<DatabaseProvider>)?.Value ?? DatabaseProvider.Sqlite;

    private void SelectSourceType(SourceType value)
    {
        _type.SelectedItem = _type.Items.Cast<DisplayItem<SourceType>>().First(x => EqualityComparer<SourceType>.Default.Equals(x.Value, value));
    }

    private void SelectProvider(DatabaseProvider value)
    {
        _provider.SelectedItem = _provider.Items.Cast<DisplayItem<DatabaseProvider>>().First(x => EqualityComparer<DatabaseProvider>.Default.Equals(x.Value, value));
    }

    private static string Required(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException(message);
        return value.Trim();
    }
}
