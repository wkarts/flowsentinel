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
        AppPaths paths,
        DesktopSettingsService settingsService)
    {
        _store = store;
        _automationControl = automationControl;
        _secretProtector = secretProtector;
        _evolutionInstanceService = evolutionInstanceService;
        _sourceDesigner = sourceDesigner;
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
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true
        };

        AddButton(toolbar, "Atualizar", async (_, _) => await RefreshAsync());
        AddButton(toolbar, "Executar agora", async (_, _) => await ExecuteSelectedAsync());
        AddButton(toolbar, "Nova automação", async (_, _) => await CreateAutomationAsync());
        AddButton(toolbar, "Editar", async (_, _) => await EditAutomationAsync());
        AddButton(toolbar, "JSON avançado", async (_, _) => await EditAutomationJsonAsync());
        AddButton(toolbar, "Ativar/Desativar", async (_, _) => await ToggleAutomationAsync());
        AddButton(toolbar, "Importar", async (_, _) => await ImportAutomationAsync());
        AddButton(toolbar, "Exportar", async (_, _) => await ExportAutomationAsync());
        AddButton(toolbar, "Excluir", async (_, _) => await DeleteAutomationAsync());
        AddButton(toolbar, "Canais", async (_, _) => await ManageChannelsAsync());
        AddButton(toolbar, "Configurações", (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        AddButton(toolbar, "Sobre", (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty));
        AddButton(toolbar, "Dados", (_, _) => OpenFolder(_paths.DataDirectory));
        AddButton(toolbar, "Logs", (_, _) => OpenFolder(_paths.LogDirectory));

        _summary.Dock = DockStyle.Top;
        _summary.Height = 42;
        _summary.Padding = new Padding(12, 10, 0, 0);
        _summary.Font = new Font(Font, FontStyle.Bold);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.Name), HeaderText = "Automação", FillWeight = 180 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(AutomationRow.Enabled), HeaderText = "Ativa", FillWeight = 35 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.Interval), HeaderText = "Intervalo", FillWeight = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.LastRun), HeaderText = "Última execução", FillWeight = 85 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.NextRun), HeaderText = "Próxima execução", FillWeight = 85 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(AutomationRow.Status), HeaderText = "Situação", FillWeight = 120 });
        _grid.CellDoubleClick += async (_, _) => await EditAutomationAsync();

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        Controls.Add(_grid);
        Controls.Add(_summary);
        Controls.Add(toolbar);
        Controls.Add(statusStrip);
    }

    private static void AddButton(Control parent, string text, EventHandler handler)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 32,
            Margin = new Padding(3)
        };
        button.Click += handler;
        parent.Controls.Add(button);
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

    private async Task CreateAutomationAsync()
    {
        var definition = CreateTemplate();
        using var editor = new AutomationWizardForm(definition, _store, _sourceDesigner, _secretProtector);
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

        using var editor = new AutomationWizardForm(definition, _store, _sourceDesigner, _secretProtector);
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
