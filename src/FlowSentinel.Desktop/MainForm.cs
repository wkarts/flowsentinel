using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using FlowSentinel.Infrastructure;

namespace FlowSentinel.Desktop;

internal sealed class MainForm : Form
{
    private readonly IFlowStore _store;
    private readonly IAutomationControl _automationControl;
    private readonly ISecretProtector _secretProtector;
    private readonly IEvolutionInstanceService _evolutionInstanceService;
    private readonly ISourceDesignerService _sourceDesigner;
    private readonly IWorkbookMonitoringService _workbookMonitoringService;
    private readonly IContactDirectory _contactDirectory;
    private readonly AppPaths _paths;
    private readonly DesktopSettingsService _settingsService;
    private readonly DataGridView _grid = new();
    private readonly Label _summary = new();
    private readonly ToolStripStatusLabel _status = new("Pronto");

    private bool _allowClose;

    internal event EventHandler? SettingsRequested;
    internal event EventHandler? AboutRequested;
    internal event EventHandler? ExitRequested;

    public MainForm(
        IFlowStore store,
        IAutomationControl automationControl,
        ISecretProtector secretProtector,
        IEvolutionInstanceService evolutionInstanceService,
        ISourceDesignerService sourceDesigner,
        IWorkbookMonitoringService workbookMonitoringService,
        IContactDirectory contactDirectory,
        AppPaths paths,
        DesktopSettingsService settingsService)
    {
        _store = store;
        _automationControl = automationControl;
        _secretProtector = secretProtector;
        _evolutionInstanceService = evolutionInstanceService;
        _sourceDesigner = sourceDesigner;
        _workbookMonitoringService = workbookMonitoringService;
        _contactDirectory = contactDirectory;
        _paths = paths;
        _settingsService = settingsService;

        Text = "FlowSentinel - Monitoramento e Notificações";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 600);
        Size = new Size(1180, 720);
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? SystemIcons.Application;

        BuildLayout();
        Load += async (_, _) => await RefreshAsync();
        FormClosing += OnFormClosing;
    }

    internal void PrepareForExit() => _allowClose = true;

    public async Task ExecuteSelectedAsync()
    {
        var item = SelectedItem();
        if (item is null)
        {
            return;
        }

        await RunBusyAsync("Executando automação...", async () =>
        {
            await _automationControl.ExecuteNowAsync(item.Id, CancellationToken.None);
            await RefreshAsync();
        });
    }

    private void BuildLayout()
    {
        var menu = BuildMainMenu();
        var toolbar = BuildToolbar();
        var header = BuildDashboardHeader();

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.RowHeadersVisible = false;
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersHeight = 36;
        _grid.RowTemplate.Height = 32;
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.Name), HeaderText = "Automação", FillWeight = 190 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(AutomationRow.Enabled), HeaderText = "Ativa", FillWeight = 38 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.Interval), HeaderText = "Intervalo", FillWeight = 58 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.LastRun), HeaderText = "Última execução", FillWeight = 88 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.NextRun), HeaderText = "Próxima execução", FillWeight = 88 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.Status), HeaderText = "Situação", FillWeight = 115 });
        _grid.CellDoubleClick += async (_, _) => await EditAutomationAsync();

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 14) };
        content.Controls.Add(_grid);

        var statusStrip = new StatusStrip();
        statusStrip.SizingGrip = true;
        statusStrip.Items.Add(_status);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(menu, 0, 0);
        root.Controls.Add(toolbar, 0, 1);
        root.Controls.Add(header, 0, 2);
        root.Controls.Add(content, 0, 3);
        root.Controls.Add(statusStrip, 0, 4);
        Controls.Add(root);
        MainMenuStrip = menu;
    }

    private MenuStrip BuildMainMenu()
    {
        var menu = new MenuStrip { Dock = DockStyle.Top, Padding = new Padding(8, 3, 0, 3) };

        var file = new ToolStripMenuItem("Arquivo");
        AddMenuItem(file.DropDownItems, "Importar automação...", async (_, _) => await ImportAutomationAsync());
        AddMenuItem(file.DropDownItems, "Exportar automação selecionada...", async (_, _) => await ExportAutomationAsync());
        file.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(file.DropDownItems, "Abrir pasta de dados", (_, _) => OpenFolder(_paths.DataDirectory));
        AddMenuItem(file.DropDownItems, "Abrir pasta de logs", (_, _) => OpenFolder(_paths.LogDirectory));
        file.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(file.DropDownItems, "Sair", (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        var automations = new ToolStripMenuItem("Automações");
        AddMenuItem(automations.DropDownItems, "Nova automação avançada...", async (_, _) => await CreateAutomationAsync());
        AddMenuItem(automations.DropDownItems, "Editar automação selecionada...", async (_, _) => await EditAutomationAsync());
        AddMenuItem(automations.DropDownItems, "Editar JSON avançado...", async (_, _) => await EditAutomationJsonAsync());
        automations.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(automations.DropDownItems, "Executar agora", async (_, _) => await ExecuteSelectedAsync());
        AddMenuItem(automations.DropDownItems, "Ativar ou desativar", async (_, _) => await ToggleAutomationAsync());
        AddMenuItem(automations.DropDownItems, "Excluir...", async (_, _) => await DeleteAutomationAsync());

        var spreadsheets = new ToolStripMenuItem("Planilhas");
        AddMenuItem(spreadsheets.DropDownItems, "Novo monitoramento personalizado...", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.Custom));
        AddMenuItem(spreadsheets.DropDownItems, "Painel de monitoramento", (_, _) => OpenWorkbookMonitoring());

        var models = BuildModelsMenu("Modelos");

        var contacts = new ToolStripMenuItem("Contatos");
        AddMenuItem(contacts.DropDownItems, "Novo contato...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.NewContact));
        AddMenuItem(contacts.DropDownItems, "Novo grupo de contatos...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.NewGroup));
        contacts.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(contacts.DropDownItems, "Catálogo de contatos...", async (_, _) => await ManageContactsAsync());
        AddMenuItem(contacts.DropDownItems, "Grupos de contatos...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.ShowGroups));
        contacts.DropDownItems.Add(new ToolStripSeparator());
        var importContacts = new ToolStripMenuItem("Importar");
        AddMenuItem(importContacts.DropDownItems, "Catálogo JSON...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.ImportJson));
        AddMenuItem(importContacts.DropDownItems, "Contatos CSV...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.ImportCsv));
        contacts.DropDownItems.Add(importContacts);
        var exportContacts = new ToolStripMenuItem("Exportar");
        AddMenuItem(exportContacts.DropDownItems, "Catálogo JSON...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.ExportJson));
        AddMenuItem(exportContacts.DropDownItems, "Contatos CSV...", async (_, _) => await ManageContactsAsync(ContactManagerStartAction.ExportCsv));
        contacts.DropDownItems.Add(exportContacts);

        var settings = new ToolStripMenuItem("Configurações");
        AddMenuItem(settings.DropDownItems, "Canais de notificação...", async (_, _) => await ManageChannelsAsync());
        AddMenuItem(settings.DropDownItems, "Contatos e grupos...", async (_, _) => await ManageContactsAsync());
        AddMenuItem(settings.DropDownItems, "Preferências do aplicativo...", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));

        var help = new ToolStripMenuItem("Ajuda");
        AddMenuItem(help.DropDownItems, "Sobre o FlowSentinel", (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty));

        menu.Items.AddRange([file, automations, spreadsheets, models, contacts, settings, help]);
        return menu;
    }

    private ToolStrip BuildToolbar()
    {
        var toolbar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(10, 6, 10, 6),
            AutoSize = true,
            ImageScalingSize = new Size(20, 20)
        };

        var create = new ToolStripDropDownButton("Novo");
        AddMenuItem(create.DropDownItems, "Assistente de planilhas", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.Custom));
        AddMenuItem(create.DropDownItems, "Automação avançada", async (_, _) => await CreateAutomationAsync());
        toolbar.Items.Add(create);

        var models = new ToolStripDropDownButton("Modelos");
        PopulateModels(models.DropDownItems);
        toolbar.Items.Add(models);
        toolbar.Items.Add(new ToolStripSeparator());
        AddToolbarButton(toolbar, "Executar", async (_, _) => await ExecuteSelectedAsync());
        AddToolbarButton(toolbar, "Editar", async (_, _) => await EditAutomationAsync());
        AddToolbarButton(toolbar, "Ativar/Desativar", async (_, _) => await ToggleAutomationAsync());
        toolbar.Items.Add(new ToolStripSeparator());
        AddToolbarButton(toolbar, "Planilhas", (_, _) => OpenWorkbookMonitoring());
        AddToolbarButton(toolbar, "Canais", async (_, _) => await ManageChannelsAsync());
        AddToolbarButton(toolbar, "Contatos", async (_, _) => await ManageContactsAsync());
        toolbar.Items.Add(new ToolStripSeparator());
        AddToolbarButton(toolbar, "Atualizar", async (_, _) => await RefreshAsync());
        return toolbar;
    }

    private Control BuildDashboardHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(18, 14, 18, 12),
            BackColor = Color.FromArgb(244, 247, 251)
        };
        var title = new Label
        {
            Text = "Central de monitoramento",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 16F, FontStyle.Bold),
            ForeColor = Color.FromArgb(35, 50, 70)
        };
        var subtitle = new Label
        {
            Text = "Acompanhe automações, ocorrências e entregas em uma única visão.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 2, 0, 8)
        };
        _summary.AutoSize = true;
        _summary.Font = new Font(Font.FontFamily, 9.5F, FontStyle.Bold);
        _summary.ForeColor = Color.FromArgb(35, 65, 95);
        _summary.Padding = Padding.Empty;
        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(subtitle, 0, 1);
        panel.Controls.Add(_summary, 0, 2);
        return panel;
    }

    private ToolStripMenuItem BuildModelsMenu(string text)
    {
        var item = new ToolStripMenuItem(text);
        PopulateModels(item.DropDownItems);
        return item;
    }

    private void PopulateModels(ToolStripItemCollection items)
    {
        AddMenuItem(items, "Matriz contábil RP-102...", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.Rp102));
        AddMenuItem(items, "Matriz de acompanhamento por períodos...", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.PeriodicMatrix));
        AddMenuItem(items, "Controle de tarefas e responsáveis...", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.TaskTracking));
        AddMenuItem(items, "Controle documental...", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.DocumentControl));
        items.Add(new ToolStripSeparator());
        AddMenuItem(items, "Modelo personalizado...", async (_, _) => await CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind.Custom));
    }

    private static void AddMenuItem(ToolStripItemCollection items, string text, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += handler;
        items.Add(item);
    }

    private static void AddToolbarButton(ToolStrip toolbar, string text, EventHandler handler)
    {
        var button = new ToolStripButton(text) { DisplayStyle = ToolStripItemDisplayStyle.Text, AutoSize = true };
        button.Click += handler;
        toolbar.Items.Add(button);
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync("Atualizando...", async () =>
        {
            var automations = await _store.GetAutomationsAsync(CancellationToken.None);
            _grid.DataSource = automations.Select(x => new AutomationRow
            {
                Id = x.Id,
                Name = x.Name,
                Enabled = x.Enabled,
                Interval = TimeSpan.FromSeconds(x.IntervalSeconds).ToString(),
                LastRun = x.LastRunAt?.LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss") ?? "-",
                NextRun = x.NextRunAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss"),
                Status = string.IsNullOrWhiteSpace(x.LastError) ? "Normal" : x.LastError
            }).ToList();

            var snapshot = await _store.GetDashboardSnapshotAsync(CancellationToken.None);
            _summary.Text = $"Automações ativas: {snapshot.EnabledAutomations}    |    Ocorrências abertas: {snapshot.ActiveOccurrences}    |    Entregas pendentes: {snapshot.PendingDeliveries}    |    Falhas: {snapshot.FailedDeliveries}";
        });
    }

    private async Task CreateStructuredWorkbookMonitoringAsync(WorkbookTemplateKind templateKind = WorkbookTemplateKind.Rp102)
    {
        try
        {
            var channels = await _store.GetChannelConfigurationsAsync(CancellationToken.None);
            var contacts = await _contactDirectory.GetSnapshotAsync(CancellationToken.None);
            using var wizard = new StructuredWorkbookWizardForm(
                channels.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray(),
                _workbookMonitoringService,
                contacts,
                templateKind);
            if (wizard.ShowDialog(this) == DialogResult.OK && wizard.Definition is not null)
            {
                await _store.SaveAutomationAsync(wizard.Definition, CancellationToken.None);
                await RefreshAsync();
                if (MessageBox.Show(
                        this,
                        "Monitoramento criado. Deseja abrir agora o painel administrativo da planilha?",
                        "FlowSentinel",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    OpenWorkbookMonitoring();
                }
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void OpenWorkbookMonitoring()
    {
        using var panel = new WorkbookMonitorForm(_store, _workbookMonitoringService);
        panel.ShowDialog(this);
    }

    private async Task CreateAutomationAsync()
    {
        var definition = CreateTemplate();
        var contacts = await _contactDirectory.GetSnapshotAsync(CancellationToken.None);
        using var editor = new AutomationWizardForm(definition, _store, _sourceDesigner, _secretProtector, contacts);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            await _store.SaveAutomationAsync(editor.Definition, CancellationToken.None);
            await RefreshAsync();
        }
    }

    private async Task EditAutomationAsync()
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }

        var definition = await _store.GetAutomationDefinitionAsync(selected.Id, CancellationToken.None);
        if (definition is null)
        {
            return;
        }

        var contacts = await _contactDirectory.GetSnapshotAsync(CancellationToken.None);
        using var editor = new AutomationWizardForm(definition, _store, _sourceDesigner, _secretProtector, contacts);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            await _store.SaveAutomationAsync(editor.Definition, CancellationToken.None);
            await RefreshAsync();
        }
    }

    private async Task EditAutomationJsonAsync()
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }

        var definition = await _store.GetAutomationDefinitionAsync(selected.Id, CancellationToken.None);
        if (definition is null)
        {
            return;
        }

        using var editor = new AutomationJsonEditorForm(definition, _secretProtector);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Definition is not null)
        {
            await _store.SaveAutomationAsync(editor.Definition, CancellationToken.None);
            await RefreshAsync();
        }
    }

    private async Task ToggleAutomationAsync()
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }
        var definition = await _store.GetAutomationDefinitionAsync(selected.Id, CancellationToken.None);
        if (definition is null)
        {
            return;
        }
        definition.Enabled = !definition.Enabled;
        await _store.SaveAutomationAsync(definition, CancellationToken.None);
        await RefreshAsync();
    }

    private async Task ImportAutomationAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Definição JSON (*.json)|*.json|Todos os arquivos (*.*)|*.*",
            Title = "Importar automação"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName);
            var definition = JsonSerializer.Deserialize<AutomationDefinition>(json, FlowJson.Options)
                             ?? throw new InvalidOperationException("O arquivo não contém uma automação válida.");
            definition.Validate();
            await _store.SaveAutomationAsync(definition, CancellationToken.None);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task ExportAutomationAsync()
    {
        var selected = SelectedItem();
        if (selected is null)
        {
            return;
        }
        var definition = await _store.GetAutomationDefinitionAsync(selected.Id, CancellationToken.None);
        if (definition is null)
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Definição JSON (*.json)|*.json",
            FileName = $"{SanitizeFileName(definition.Name)}.json"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(definition, FlowJson.Options));
        }
    }

    private async Task DeleteAutomationAsync()
    {
        var selected = SelectedItem();
        if (selected is null || MessageBox.Show(
                this,
                $"Excluir a automação '{selected.Name}' e todo o histórico relacionado?",
                "Confirmação",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        await _store.DeleteAutomationAsync(selected.Id, CancellationToken.None);
        await RefreshAsync();
    }

    private async Task ManageChannelsAsync()
    {
        using var form = new ChannelManagerForm(_store, _secretProtector, _evolutionInstanceService);
        form.ShowDialog(this);
        await RefreshAsync();
    }

    private async Task ManageContactsAsync(ContactManagerStartAction startAction = ContactManagerStartAction.None)
    {
        using var form = new ContactManagerForm(_contactDirectory, _store, startAction);
        form.ShowDialog(this);
        await RefreshAsync();
    }

    private AutomationRow? SelectedItem() => _grid.CurrentRow?.DataBoundItem as AutomationRow;

    private async Task RunBusyAsync(string status, Func<Task> operation)
    {
        try
        {
            UseWaitCursor = true;
            _status.Text = status;
            await operation();
            _status.Text = "Pronto";
        }
        catch (Exception exception)
        {
            _status.Text = "Erro";
            ShowError(exception);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_allowClose || eventArgs.CloseReason != CloseReason.UserClosing)
        {
            return;
        }

        var settings = _settingsService.Current;
        eventArgs.Cancel = true;

        switch (settings.CloseBehavior)
        {
            case DesktopCloseBehavior.MinimizeToTray:
                Hide();
                return;

            case DesktopCloseBehavior.Ask:
                var result = MessageBox.Show(
                    this,
                    "Deseja encerrar o FlowSentinel?\n\nSim: encerrar\nNão: minimizar para o tray",
                    "Fechar FlowSentinel",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    RequestApplicationExit();
                }
                else if (result == DialogResult.No)
                {
                    Hide();
                }
                return;

            case DesktopCloseBehavior.Exit:
                if (!settings.ConfirmBeforeExit || MessageBox.Show(
                        this,
                        "Encerrar o FlowSentinel e interromper o processamento do Desktop?",
                        "Confirmação",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RequestApplicationExit();
                }
                return;
        }
    }

    private void RequestApplicationExit()
    {
        BeginInvoke(new Action(() => ExitRequested?.Invoke(this, EventArgs.Empty)));
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    private static string SanitizeFileName(string value) =>
        string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));

    private void ShowError(Exception exception) => MessageBox.Show(
        this,
        exception.Message,
        "FlowSentinel",
        MessageBoxButtons.OK,
        MessageBoxIcon.Error);

    private static AutomationDefinition CreateTemplate()
    {
        using var configurationDocument = JsonDocument.Parse("""
        {
          "filePath": "C:\\Dados\\clientes.csv",
          "delimiter": ";",
          "quote": "\"",
          "encoding": "utf-8",
          "hasHeader": true,
          "ignoreEmptyLines": true
        }
        """);

        return new AutomationDefinition
        {
            Name = "Nova automação",
            Description = "Edite a fonte, os critérios, as ações, os canais e os destinatários.",
            Enabled = false,
            IntervalSeconds = 300,
            Sources =
            [
                new DataSourceDefinition
                {
                    Alias = "primary",
                    Name = "Fonte principal",
                    Type = SourceType.Csv,
                    IsPrimary = true,
                    KeyFields = ["Id"],
                    Configuration = configurationDocument.RootElement.Clone()
                }
            ],
            EntryRules = new RuleSetDefinition
            {
                Type = RuleSetType.Entry,
                Root = new RuleGroupDefinition
                {
                    Operator = LogicalOperator.And,
                    Rules =
                    [
                        new RuleDefinition
                        {
                            Field = "Status",
                            Operator = RuleOperator.Equal,
                            ExpectedValue = "Pendente"
                        }
                    ]
                }
            },
            CompletionRules = new RuleSetDefinition
            {
                Type = RuleSetType.Completion,
                Root = new RuleGroupDefinition
                {
                    Operator = LogicalOperator.Or,
                    Rules =
                    [
                        new RuleDefinition
                        {
                            Field = "Status",
                            Operator = RuleOperator.Equal,
                            ExpectedValue = "Concluido"
                        }
                    ]
                }
            },
            Actions =
            [
                new ActionDefinition
                {
                    Name = "Notificação local",
                    Trigger = ActionTrigger.OnOpen,
                    SubjectTemplate = "Ocorrência: {{record.key}}",
                    MessageTemplate = "O registro {{record.key}} atendeu aos critérios da automação.",
                    Channels =
                    [
                        new ActionChannelDefinition
                        {
                            ChannelConfigurationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                            ChannelType = ChannelType.LocalWindows,
                            Order = 1,
                            Required = true
                        }
                    ]
                }
            ]
        };
    }

    private sealed class AutomationRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public string Interval { get; init; } = string.Empty;
        public string LastRun { get; init; } = string.Empty;
        public string NextRun { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }
}
