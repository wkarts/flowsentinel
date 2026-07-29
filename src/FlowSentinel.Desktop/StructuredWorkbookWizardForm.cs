using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class StructuredWorkbookWizardForm : Form
{
    private readonly IReadOnlyList<ChannelConfiguration> _channels;
    private readonly IWorkbookMonitoringService _monitoringService;
    private readonly TextBox _name = new();
    private readonly TextBox _filePath = new();
    private readonly ComboBox _worksheetSelection = new();
    private readonly TextBox _worksheet = new();
    private readonly NumericUpDown _intervalMinutes = new();
    private readonly CheckBox _includeBlanks = new();
    private readonly CheckBox _monitorFormatting = new();
    private readonly CheckBox _statusChanges = new();
    private readonly CheckBox _currentStatusChanges = new();
    private readonly CheckBox _collaboratorChanges = new();
    private readonly CheckBox _countChanges = new();
    private readonly DataGridView _channelGrid = new();
    private readonly Label _analysisSummary = new();

    internal AutomationDefinition? Definition { get; private set; }

    internal StructuredWorkbookWizardForm(
        IReadOnlyList<ChannelConfiguration> channels,
        IWorkbookMonitoringService monitoringService)
    {
        _channels = channels;
        _monitoringService = monitoringService;
        Text = "Modelo opcional RP-102 para planilha estruturada";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(900, 720);
        MinimumSize = new Size(820, 620);
        ConfigureControls();
        BuildLayout();
    }

    private void ConfigureControls()
    {
        _name.Text = "Monitoramento RP-102";
        _filePath.Dock = DockStyle.Fill;
        _worksheetSelection.DropDownStyle = ComboBoxStyle.DropDownList;
        _worksheetSelection.Items.AddRange(["Aba mais recente pelo ano", "Aba selecionada", "Todas as abas com ano"]);
        _worksheetSelection.SelectedIndex = 0;
        _worksheetSelection.SelectedIndexChanged += (_, _) => _worksheet.Enabled = _worksheetSelection.SelectedIndex == 1;
        _worksheet.Enabled = false;
        _worksheet.PlaceholderText = "Ex.: Controle 2026";

        _intervalMinutes.Minimum = 1;
        _intervalMinutes.Maximum = 1440;
        _intervalMinutes.Value = 5;
        _includeBlanks.Text = "Incluir células vazias nas contagens";
        _monitorFormatting.Text = "Monitorar cores e destaques das células";
        _statusChanges.Text = "Notificar mudança em qualquer célula de acompanhamento por registro e coluna";
        _currentStatusChanges.Text = "Notificar mudança do valor atual do registro (última coluna aplicável)";
        _collaboratorChanges.Text = "Notificar mudança de responsável";
        _countChanges.Text = "Notificar alteração das quantidades agregadas por valor";
        foreach (var checkbox in new[] { _includeBlanks, _monitorFormatting, _statusChanges, _currentStatusChanges, _collaboratorChanges, _countChanges })
        {
            checkbox.AutoSize = true;
            checkbox.Checked = true;
        }

        _channelGrid.AllowUserToAddRows = false;
        _channelGrid.AllowUserToDeleteRows = false;
        _channelGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _channelGrid.RowHeadersVisible = false;
        _channelGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Usar", FillWeight = 25 });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Canal", ReadOnly = true, FillWeight = 90 });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Tipo", ReadOnly = true, FillWeight = 60 });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recipient", HeaderText = "Destinatário padrão", FillWeight = 130 });
        foreach (var channel in _channels.Where(x => x.Enabled).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var index = _channelGrid.Rows.Add(
                channel.Type == ChannelType.LocalWindows,
                channel.Id,
                channel.Name,
                VisualEditorSupport.ChannelTypeText(channel.Type),
                channel.Type == ChannelType.LocalWindows ? "local" : string.Empty);
            _channelGrid.Rows[index].Tag = channel;
            if (channel.Type == ChannelType.LocalWindows)
            {
                _channelGrid.Rows[index].Cells["Recipient"].ReadOnly = true;
            }
        }
    }

    private void BuildLayout()
    {
        var main = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 4
        };
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        main.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var intro = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(840, 0),
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
            Text = "Este é um modelo opcional baseado na planilha RP-102 enviada. Ele não altera o motor genérico: para outros formatos, use 'Nova automação avançada' e configure a fonte no modo matriz estruturada. Mesmo neste modelo, toda a planilha é monitorada por uma única automação; não é necessário cadastrar empresa por empresa."
        };

        var fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(0, 12, 0, 12) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        AddField(fields, "Nome do monitoramento", _name);
        var fileActions = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var fileButton = new Button { Text = "Procurar...", AutoSize = true };
        fileButton.Click += (_, _) => BrowseFile();
        var analyzeButton = new Button { Text = "Analisar estrutura", AutoSize = true };
        analyzeButton.Click += async (_, _) => await AnalyzeStructureAsync();
        fileActions.Controls.Add(fileButton);
        fileActions.Controls.Add(analyzeButton);
        AddField(fields, "Planilha Excel", _filePath, fileActions);
        AddField(fields, "Como selecionar a aba", _worksheetSelection);
        AddField(fields, "Aba específica", _worksheet);
        AddField(fields, "Intervalo em minutos", _intervalMinutes);
        _analysisSummary.AutoSize = true;
        _analysisSummary.MaximumSize = new Size(820, 0);
        _analysisSummary.ForeColor = Color.DimGray;
        _analysisSummary.Text = "Selecione a planilha e clique em 'Analisar estrutura' para conferir registros, grupos, colunas de acompanhamento e avisos antes de criar.";
        fields.Controls.Add(_analysisSummary, 0, fields.RowCount);
        fields.SetColumnSpan(_analysisSummary, 3);
        fields.RowCount++;

        var options = new GroupBox { Text = "O que monitorar", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12) };
        var optionFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        optionFlow.Controls.Add(_statusChanges);
        optionFlow.Controls.Add(_currentStatusChanges);
        optionFlow.Controls.Add(_collaboratorChanges);
        optionFlow.Controls.Add(_countChanges);
        optionFlow.Controls.Add(_includeBlanks);
        optionFlow.Controls.Add(_monitorFormatting);
        options.Controls.Add(optionFlow);

        var notifications = new GroupBox { Text = "Canais e destinatários", Dock = DockStyle.Fill, Padding = new Padding(8) };
        var notificationLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        notificationLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        notificationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        notificationLayout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Marque os canais. WhatsApp, Telegram e e-mail precisam do destinatário correspondente. Sem canais, o painel administrativo continuará funcionando sem disparos."
        }, 0, 0);
        _channelGrid.Dock = DockStyle.Fill;
        notificationLayout.Controls.Add(_channelGrid, 0, 1);
        notifications.Controls.Add(notificationLayout);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var create = new Button { Text = "Criar monitoramento", AutoSize = true };
        var cancel = new Button { Text = "Cancelar", AutoSize = true, DialogResult = DialogResult.Cancel };
        create.Click += (_, _) => CreateDefinition();
        buttons.Controls.Add(create);
        buttons.Controls.Add(cancel);
        AcceptButton = create;
        CancelButton = cancel;

        var upper = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        upper.Controls.Add(fields);
        upper.Controls.Add(options);

        main.Controls.Add(intro, 0, 0);
        main.Controls.Add(upper, 0, 1);
        main.Controls.Add(notifications, 0, 2);
        main.Controls.Add(buttons, 0, 3);
        Controls.Add(main);
    }

    private static void AddField(TableLayoutPanel panel, string label, Control control, Control? extra = null)
    {
        var row = panel.RowCount++;
        panel.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        panel.Controls.Add(control, 1, row);
        if (extra is not null)
        {
            panel.Controls.Add(extra, 2, row);
        }
    }

    private void BrowseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Planilhas Excel (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _filePath.Text = dialog.FileName;
        }
    }


    private async Task AnalyzeStructureAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_filePath.Text) || !File.Exists(_filePath.Text.Trim()))
            {
                throw new InvalidOperationException("Selecione uma planilha Excel existente.");
            }

            UseWaitCursor = true;
            _analysisSummary.Text = "Analisando a estrutura da planilha...";
            var analysis = await _monitoringService.AnalyzeAsync(BuildSource(), CancellationToken.None);
            _analysisSummary.Text = $"Estrutura reconhecida: {analysis.EntityCount:N0} registros, {analysis.SectionCount:N0} grupos, " +
                                    $"{analysis.StatusCellCount:N0} células de situação, {analysis.Worksheets.Count:N0} aba(s) e " +
                                    $"{analysis.Warnings.Count:N0} aviso(s). A planilha inteira será monitorada em uma única automação.";
        }
        catch (Exception exception)
        {
            _analysisSummary.Text = "Não foi possível reconhecer a estrutura. Revise o arquivo ou use a configuração avançada da fonte.";
            VisualEditorSupport.ShowError(this, exception, "Análise da planilha");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void CreateDefinition()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                throw new InvalidOperationException("Informe o nome do monitoramento.");
            }
            if (string.IsNullOrWhiteSpace(_filePath.Text) || !File.Exists(_filePath.Text.Trim()))
            {
                throw new InvalidOperationException("Selecione uma planilha Excel existente.");
            }

            var selectedChannels = new List<(ChannelConfiguration Configuration, string Recipient)>();
            foreach (DataGridViewRow row in _channelGrid.Rows)
            {
                if (row.Tag is not ChannelConfiguration channel || Convert.ToBoolean(row.Cells["Use"].Value) != true)
                {
                    continue;
                }
                var recipient = Convert.ToString(row.Cells["Recipient"].Value)?.Trim() ?? string.Empty;
                if (channel.Type != ChannelType.LocalWindows && string.IsNullOrWhiteSpace(recipient))
                {
                    throw new InvalidOperationException($"Informe o destinatário do canal '{channel.Name}'.");
                }
                selectedChannels.Add((channel, recipient));
            }

            var source = BuildSource();
            var actions = new List<ActionDefinition>();
            if (selectedChannels.Count > 0)
            {
                if (_statusChanges.Checked)
                {
                    actions.Add(BuildAction(
                        "Mudança de situação",
                        selectedChannels,
                        [Equal("__recordType", "Status"), Changed("Status")],
                        "{{Entity}} - {{Period}}",
                        "A situação de {{Entity}} (chave {{Code}}), grupo {{Category}}, período {{Period}}, mudou de '{{previous.Status}}' para '{{Status}}'. Célula: {{CellAddress}}. Responsável: {{Owner}}."));
                }
                if (_currentStatusChanges.Checked)
                {
                    actions.Add(BuildAction(
                        "Mudança do valor atual do registro",
                        selectedChannels,
                        [Equal("__recordType", "Entity"), Changed("CurrentValue")],
                        "Situação atual alterada - {{Entity}}",
                        "A situação atual de {{Entity}} (chave {{Code}}), grupo {{Category}}, mudou de '{{previous.CurrentValue}}' para '{{CurrentValue}}'. Período atual: {{CurrentPeriod}}. Responsável: {{Owner}}."));
                }
                if (_collaboratorChanges.Checked)
                {
                    actions.Add(BuildAction(
                        "Mudança de colaborador",
                        selectedChannels,
                        [Equal("__recordType", "Entity"), Changed("Owner")],
                        "Mudança de responsável - {{Entity}}",
                        "O registro {{Entity}} (chave {{Code}}), grupo {{Category}}, mudou do responsável '{{previous.Owner}}' para '{{Owner}}'."));
                }
                if (_countChanges.Checked)
                {
                    actions.Add(BuildAction(
                        "Mudança de quantidade por situação",
                        selectedChannels,
                        [Equal("__recordType", "Aggregate"), Changed("Count")],
                        "Quantidade alterada - {{Period}} / {{StatusDisplay}}",
                        "O indicador {{Metric}} ({{Unit}}), agrupamento {{Scope}} / {{Group}}, período {{Period}}, situação {{StatusDisplay}}, mudou de {{previous.Count}} para {{Count}}."));
                }
                if (_monitorFormatting.Checked)
                {
                    actions.Add(BuildFormattingAction(selectedChannels));
                }
            }

            Definition = new AutomationDefinition
            {
                Id = Guid.NewGuid(),
                Name = _name.Text.Trim(),
                Description = "Monitoramento administrativo de matriz estruturada com múltiplos grupos, entidades, colunas, cores e totais por valor.",
                Enabled = true,
                IntervalSeconds = (int)_intervalMinutes.Value * 60,
                Priority = 100,
                MissingRecordBehavior = MissingRecordBehavior.Resolve,
                ResolveWhenPersistenceFails = false,
                Sources = [source],
                EntryRules = RuleSetDefinition.AlwaysTrue(RuleSetType.Entry),
                Actions = actions
            };
            Definition.Validate();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Criação do monitoramento");
        }
    }

    private DataSourceDefinition BuildSource()
    {
        var settings = new
        {
            filePath = _filePath.Text.Trim(),
            worksheet = _worksheetSelection.SelectedIndex == 1 && !string.IsNullOrWhiteSpace(_worksheet.Text) ? _worksheet.Text.Trim() : null,
            headerRow = 1,
            ignoreEmptyRows = true,
            mode = "SectionedMatrix",
            profileName = "Modelo RP-102 - Conferência contábil",
            worksheetSelection = _worksheetSelection.SelectedIndex switch
            {
                1 => "Fixed",
                2 => "AllMatching",
                _ => "LatestYear"
            },
            worksheetPattern = @"(?<year>20\d{2})",
            matrix = new
            {
                headerMarker = "Nº",
                headerTextContains = "EMPRESAS",
                periodLabels = "JAN|FEV|MAR|ABR|MAI|JUN|JUL|AGO|SET|OUT|NOV|DEZ|BAL",
                sectionTitlePrefixes = "EMPRESAS ",
                sectionNamePrefixesToRemove = "EMPRESAS ",
                numberColumn = 1,
                sectionColumn = 2,
                companyColumn = 2,
                codeColumn = 3,
                collaboratorColumn = 4,
                firstPeriodColumn = 5,
                lastPeriodColumn = 20,
                includeBlankStatuses = _includeBlanks.Checked,
                includeFormatting = _monitorFormatting.Checked,
                generateCompanyRecords = true,
                generateAggregateRecords = true,
                aggregateBySection = true,
                aggregateByCollaborator = true,
                autoDetectStandaloneSections = true,
                standaloneSectionTitles = "SIMPLES|EMPRESAS MEI|SEM MOVIMENTO",
                sectionsWithoutPeriods = "EMPRESAS MEI|SEM MOVIMENTO",
                currentStatusExcludedPeriods = "BAL",
                currentStatusMode = "CalendarPeriod",
                calendarPeriodNumbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["JAN"] = 1, ["FEV"] = 2, ["MAR"] = 3, ["ABR"] = 4,
                    ["MAI"] = 5, ["JUN"] = 6, ["JUL"] = 7, ["AGO"] = 8,
                    ["SET"] = 9, ["OUT"] = 10, ["NOV"] = 11, ["DEZ"] = 12
                },
                entitySingularName = "Empresa",
                entityPluralName = "Clientes",
                ownerName = "Colaborador",
                categoryName = "Regime",
                periodName = "Período",
                codeName = "Código",
                valueName = "Situação",
                statusLabels = new Dictionary<string, string>()
            },
            designerFields = new[]
            {
                "__recordType", "Worksheet", "Year", "Section", "Regime", "EntityKey", "Entity", "CompanyKey", "Company",
                "Code", "Owner", "Collaborator", "Category", "Period", "PeriodBase", "Status", "StatusDisplay", "StatusMeaning",
                "Value", "ValueDisplay", "CurrentPeriod", "CurrentStatus", "CurrentStatusDisplay",
                "CurrentValue", "CurrentValueDisplay", "Metric", "Unit", "Count",
                "Scope", "Group", "CellAddress", "FillColor", "IsHighlighted"
            }
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings, FlowJson.Options));
        return new DataSourceDefinition
        {
            Id = Guid.NewGuid(),
            Alias = "planilha",
            Name = "Planilha estruturada",
            Type = SourceType.Excel,
            IsPrimary = true,
            Enabled = true,
            KeyFields = ["__recordKey"],
            Configuration = document.RootElement.Clone()
        };
    }

    private static ActionDefinition BuildAction(
        string name,
        IReadOnlyCollection<(ChannelConfiguration Configuration, string Recipient)> channels,
        IReadOnlyCollection<RuleDefinition> rules,
        string subject,
        string message)
    {
        var action = new ActionDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            Enabled = true,
            Trigger = ActionTrigger.WhileActive,
            Repeat = new RepeatPolicyDefinition { Enabled = true, IntervalSeconds = 1, MaxExecutions = 0 },
            ChannelStrategy = ChannelExecutionStrategy.All,
            SuccessPolicy = ActionSuccessPolicy.AllDeliveries,
            Conditions = new RuleSetDefinition
            {
                Type = RuleSetType.ActionCondition,
                Root = new RuleGroupDefinition { Operator = LogicalOperator.And, Rules = rules.ToList() }
            },
            SubjectTemplate = subject,
            MessageTemplate = message,
            Channels = channels.Select((x, index) => new ActionChannelDefinition
            {
                ChannelConfigurationId = x.Configuration.Id,
                ChannelType = x.Configuration.Type,
                Order = index,
                Required = true
            }).ToList()
        };

        action.Recipients = channels
            .Where(x => x.Configuration.Type != ChannelType.LocalWindows)
            .Select(x => new RecipientDefinition
            {
                Type = RecipientType.Fixed,
                Value = x.Recipient,
                ChannelType = x.Configuration.Type,
                DisplayName = x.Configuration.Name
            })
            .ToList();
        return action;
    }


    private static ActionDefinition BuildFormattingAction(
        IReadOnlyCollection<(ChannelConfiguration Configuration, string Recipient)> channels)
    {
        var action = BuildAction(
            "Mudança de cor ou destaque",
            channels,
            [Equal("__recordType", "Status")],
            "Destaque alterado - {{Entity}} / {{Period}}",
            "A formatação de {{Entity}} (chave {{Code}}), grupo {{Category}}, período {{Period}}, foi alterada. Cor: '{{previous.FillColor}}' → '{{FillColor}}'; destaque: '{{previous.IsHighlighted}}' → '{{IsHighlighted}}'. Célula: {{CellAddress}}.");
        action.Conditions.Root ??= new RuleGroupDefinition { Operator = LogicalOperator.And };
        action.Conditions.Root.Groups.Add(new RuleGroupDefinition
        {
            Operator = LogicalOperator.Or,
            Rules = [Changed("FillColor"), Changed("IsHighlighted")]
        });
        return action;
    }

    private static RuleDefinition Equal(string field, string value) => new()
    {
        Field = field,
        Operator = RuleOperator.Equal,
        ExpectedValue = value
    };

    private static RuleDefinition Changed(string field) => new()
    {
        Field = field,
        Operator = RuleOperator.Changed
    };
}
