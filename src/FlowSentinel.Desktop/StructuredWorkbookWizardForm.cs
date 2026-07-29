using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class StructuredWorkbookWizardForm : Form
{
    private const string DeliveryIndividual = "Individual";
    private const string DeliveryByEntity = "Agrupar por registro";
    private const string DeliverySingleMessage = "Resumo único";

    private readonly IReadOnlyList<ChannelConfiguration> _channels;
    private readonly IWorkbookMonitoringService _monitoringService;
    private readonly ContactDirectoryDefinition _contactDirectory;
    private readonly WorkbookTemplateProfile _profile;
    private readonly Guid _automationId = Guid.NewGuid();
    private readonly Dictionary<Guid, List<RecipientDefinition>> _channelRecipients = [];

    private readonly TabControl _steps = new();
    private readonly Label _stepTitle = new();
    private readonly Label _stepDescription = new();
    private readonly Label _footerStatus = new();
    private readonly Button _back = new();
    private readonly Button _next = new();
    private readonly Button _finish = new();

    private readonly TextBox _name = new();
    private readonly TextBox _filePath = new();
    private readonly ComboBox _worksheetSelection = new();
    private readonly TextBox _worksheet = new();
    private readonly NumericUpDown _intervalMinutes = Number(1, 1440, 5);

    private readonly NumericUpDown _headerRow = Number(1, 100000, 1);
    private readonly NumericUpDown _dataStartRow = Number(0, 100000, 0);
    private readonly NumericUpDown _dataEndRow = Number(0, 100000, 0);
    private readonly NumericUpDown _numberColumn = Number(1, 500, 1);
    private readonly NumericUpDown _categoryColumn = Number(1, 500, 2);
    private readonly NumericUpDown _entityColumn = Number(1, 500, 2);
    private readonly NumericUpDown _codeColumn = Number(1, 500, 3);
    private readonly NumericUpDown _ownerColumn = Number(1, 500, 4);
    private readonly NumericUpDown _firstValueColumn = Number(1, 500, 5);
    private readonly NumericUpDown _lastValueColumn = Number(1, 500, 20);
    private readonly TextBox _headerMarker = new();
    private readonly TextBox _headerTextContains = new();
    private readonly TextBox _periodLabels = new();
    private readonly TextBox _sectionPrefixes = new();
    private readonly TextBox _sectionPrefixesToRemove = new();
    private readonly TextBox _standaloneSections = new();
    private readonly TextBox _sectionsWithoutPeriods = new();
    private readonly TextBox _excludedCurrentPeriods = new();
    private readonly ComboBox _currentValueMode = new();

    private readonly TextBox _entitySingular = new();
    private readonly TextBox _entityPlural = new();
    private readonly TextBox _ownerName = new();
    private readonly TextBox _categoryName = new();
    private readonly TextBox _periodName = new();
    private readonly TextBox _codeName = new();
    private readonly TextBox _valueName = new();

    private readonly ComboBox _previewWorksheet = new();
    private readonly DataGridView _preview = new();
    private readonly Label _analysisSummary = new();
    private readonly ListBox _warnings = new();

    private readonly CheckBox _trackBlankCells = new();
    private readonly CheckBox _monitorFormatting = new();
    private readonly CheckBox _valueChanges = new();
    private readonly CheckBox _currentValueChanges = new();
    private readonly CheckBox _ownerChanges = new();
    private readonly CheckBox _countChanges = new();
    private readonly CheckBox _aggregateGlobal = new();
    private readonly CheckBox _aggregateByCategory = new();
    private readonly CheckBox _aggregateByOwner = new();
    private readonly CheckBox _includeBlankAggregates = new();
    private readonly DataGridView _channelGrid = new();
    private readonly TextBox _review = new();

    private WorkbookMonitoringAnalysis? _analysis;

    internal AutomationDefinition? Definition { get; private set; }

    internal StructuredWorkbookWizardForm(
        IReadOnlyList<ChannelConfiguration> channels,
        IWorkbookMonitoringService monitoringService,
        ContactDirectoryDefinition contactDirectory,
        WorkbookTemplateKind templateKind = WorkbookTemplateKind.Rp102)
    {
        _channels = channels;
        _monitoringService = monitoringService;
        _contactDirectory = VisualEditorSupport.Clone(contactDirectory);
        _profile = WorkbookTemplateProfile.Get(templateKind);

        Text = $"Assistente de planilhas — {_profile.DisplayName}";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1180, 820);
        MinimumSize = new Size(1000, 700);
        Font = new Font("Segoe UI", 9F);

        ConfigureControls();
        BuildLayout();
        UpdateNavigation();
    }

    private void ConfigureControls()
    {
        _name.Text = _profile.DefaultMonitoringName;
        _filePath.PlaceholderText = "Selecione um arquivo .xlsx ou .xlsm";
        _worksheetSelection.DropDownStyle = ComboBoxStyle.DropDownList;
        _worksheetSelection.Items.AddRange(["Aba mais recente pelo ano", "Aba específica", "Todas as abas compatíveis"]);
        _worksheetSelection.SelectedIndex = _profile.Kind == WorkbookTemplateKind.Rp102 ? 0 : 1;
        _worksheetSelection.SelectedIndexChanged += (_, _) => _worksheet.Enabled = _worksheetSelection.SelectedIndex == 1;
        _worksheet.Enabled = _worksheetSelection.SelectedIndex == 1;
        _worksheet.PlaceholderText = "Nome exato da aba; pode ser preenchido após a análise";

        _headerMarker.Text = _profile.HeaderMarker;
        _headerTextContains.Text = _profile.HeaderTextContains;
        _periodLabels.Text = _profile.PeriodLabels;
        _sectionPrefixes.Text = _profile.SectionTitlePrefixes;
        _sectionPrefixesToRemove.Text = _profile.SectionNamePrefixesToRemove;
        _standaloneSections.Text = _profile.StandaloneSectionTitles;
        _sectionsWithoutPeriods.Text = _profile.SectionsWithoutPeriods;
        _excludedCurrentPeriods.Text = _profile.CurrentValueExcludedPeriods;
        SetNumber(_numberColumn, _profile.NumberColumn);
        SetNumber(_categoryColumn, _profile.CategoryColumn);
        SetNumber(_entityColumn, _profile.EntityColumn);
        SetNumber(_codeColumn, _profile.CodeColumn);
        SetNumber(_ownerColumn, _profile.OwnerColumn);
        SetNumber(_firstValueColumn, _profile.FirstValueColumn);
        SetNumber(_lastValueColumn, _profile.LastValueColumn);

        _currentValueMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _currentValueMode.Items.AddRange(["Último valor preenchido", "Período do calendário"]);
        _currentValueMode.SelectedIndex = string.Equals(_profile.CurrentValueMode, "CalendarPeriod", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        _entitySingular.Text = _profile.EntitySingular;
        _entityPlural.Text = _profile.EntityPlural;
        _ownerName.Text = _profile.OwnerName;
        _categoryName.Text = _profile.CategoryName;
        _periodName.Text = _profile.PeriodName;
        _codeName.Text = _profile.CodeName;
        _valueName.Text = _profile.ValueName;

        ConfigurePreview();
        ConfigureMonitoringOptions();
        ConfigureChannelGrid();
    }

    private void ConfigurePreview()
    {
        _previewWorksheet.DropDownStyle = ComboBoxStyle.DropDownList;
        _previewWorksheet.SelectedIndexChanged += (_, _) => RenderSelectedWorksheet();

        _preview.Dock = DockStyle.Fill;
        _preview.AllowUserToAddRows = false;
        _preview.AllowUserToDeleteRows = false;
        _preview.ReadOnly = true;
        _preview.MultiSelect = true;
        _preview.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _preview.RowHeadersWidth = 64;
        _preview.ColumnHeadersHeight = 30;
        _preview.EnableHeadersVisualStyles = false;
        _preview.BackgroundColor = SystemColors.Window;
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

        _analysisSummary.AutoSize = true;
        _analysisSummary.ForeColor = Color.DimGray;
        _analysisSummary.Text = "A análise exibirá as abas, a estrutura reconhecida e uma prévia selecionável da planilha.";

        _warnings.Dock = DockStyle.Fill;
        _warnings.IntegralHeight = false;
    }

    private void ConfigureMonitoringOptions()
    {
        _valueChanges.Text = "Mudança de valor em cada célula monitorada";
        _currentValueChanges.Text = "Mudança do valor atual de cada registro";
        _ownerChanges.Text = "Mudança de responsável";
        _monitorFormatting.Text = "Mudança de cor ou destaque";
        _countChanges.Text = "Mudança de indicadores e quantidades agregadas";
        _trackBlankCells.Text = "Acompanhar células vazias para detectar preenchimentos e limpezas";
        _aggregateGlobal.Text = "Resumo geral";
        _aggregateByCategory.Text = $"Resumo por {_profile.CategoryName.ToLowerInvariant()}";
        _aggregateByOwner.Text = $"Resumo por {_profile.OwnerName.ToLowerInvariant()}";
        _includeBlankAggregates.Text = "Incluir valores vazios nos indicadores agregados";

        _valueChanges.Checked = true;
        _currentValueChanges.Checked = true;
        _ownerChanges.Checked = true;
        _trackBlankCells.Checked = true;
        _monitorFormatting.Checked = false;
        _countChanges.Checked = false;
        _aggregateGlobal.Checked = true;
        _aggregateByCategory.Checked = false;
        _aggregateByOwner.Checked = false;
        _includeBlankAggregates.Checked = false;

        _countChanges.CheckedChanged += (_, _) => UpdateAggregateOptions();
        UpdateAggregateOptions();
    }

    private void ConfigureChannelGrid()
    {
        _channelGrid.Dock = DockStyle.Fill;
        _channelGrid.AllowUserToAddRows = false;
        _channelGrid.AllowUserToDeleteRows = false;
        _channelGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _channelGrid.RowHeadersVisible = false;
        _channelGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _channelGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Use", HeaderText = "Usar", FillWeight = 28 });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Canal", ReadOnly = true, FillWeight = 90 });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Tipo", ReadOnly = true, FillWeight = 80 });
        _channelGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Recipients", HeaderText = "Destinatários", ReadOnly = true, FillWeight = 145 });
        _channelGrid.Columns.Add(new DataGridViewButtonColumn
        {
            Name = "ConfigureRecipients",
            HeaderText = "",
            Text = "Selecionar...",
            UseColumnTextForButtonValue = true,
            FillWeight = 58
        });
        _channelGrid.Columns.Add(new DataGridViewComboBoxColumn
        {
            Name = "DeliveryMode",
            HeaderText = "Forma de envio",
            FillWeight = 100,
            Items = { DeliveryIndividual, DeliveryByEntity, DeliverySingleMessage }
        });

        foreach (var channel in _channels.Where(x => x.Enabled).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var local = channel.Type == ChannelType.LocalWindows;
            var index = _channelGrid.Rows.Add(
                local,
                channel.Id,
                channel.Name,
                VisualEditorSupport.ChannelTypeText(channel.Type),
                local ? "Notificação local" : "Nenhum destinatário selecionado",
                local ? string.Empty : "Selecionar...",
                DeliveryIndividual);
            _channelGrid.Rows[index].Tag = channel;
            _channelRecipients[channel.Id] = [];
            if (local)
            {
                _channelGrid.Rows[index].Cells["ConfigureRecipients"].ReadOnly = true;
                _channelGrid.Rows[index].Cells["DeliveryMode"].ReadOnly = true;
            }
        }
        _channelGrid.CellContentClick += (_, eventArgs) => ConfigureRecipients(eventArgs.RowIndex, eventArgs.ColumnIndex);
    }

    private void ConfigureRecipients(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || columnIndex < 0 ||
            _channelGrid.Columns[columnIndex].Name != "ConfigureRecipients" ||
            _channelGrid.Rows[rowIndex].Tag is not ChannelConfiguration channel ||
            channel.Type == ChannelType.LocalWindows)
        {
            return;
        }

        using var selector = new RecipientSelectionForm(
            _automationId,
            channel.Type,
            _contactDirectory,
            _channelRecipients.GetValueOrDefault(channel.Id, []));
        if (selector.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _channelRecipients[channel.Id] = selector.Recipients.ToList();
        _channelGrid.Rows[rowIndex].Cells["Recipients"].Value = RecipientSummary(selector.Recipients);
        if (selector.Recipients.Count > 0)
        {
            _channelGrid.Rows[rowIndex].Cells["Use"].Value = true;
        }
        UpdateReview();
    }

    private static string RecipientSummary(IReadOnlyCollection<RecipientDefinition> recipients)
    {
        if (recipients.Count == 0)
        {
            return "Nenhum destinatário selecionado";
        }

        var contacts = recipients.Count(x => x.Type == RecipientType.Contact);
        var groups = recipients.Count(x => x.Type == RecipientType.Group);
        var manual = recipients
            .Where(x => x.Type == RecipientType.Fixed)
            .SelectMany(x => (x.Value ?? string.Empty).Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var parts = new List<string>();
        if (contacts > 0) parts.Add($"{contacts} contato(s)");
        if (groups > 0) parts.Add($"{groups} grupo(s)");
        if (manual > 0) parts.Add($"{manual} manual(is)");
        return string.Join(", ", parts);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildSteps(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(4, 2, 4, 12)
        };

        _stepTitle.AutoSize = true;
        _stepTitle.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
        _stepTitle.Text = "Assistente de monitoramento de planilhas";

        _stepDescription.AutoSize = true;
        _stepDescription.MaximumSize = new Size(1080, 0);
        _stepDescription.ForeColor = Color.DimGray;
        _stepDescription.Text = $"Modelo selecionado: {_profile.DisplayName}. {_profile.Description}";

        header.Controls.Add(_stepTitle, 0, 0);
        header.Controls.Add(_stepDescription, 0, 1);
        return header;
    }

    private Control BuildSteps()
    {
        _steps.Dock = DockStyle.Fill;
        _steps.Appearance = TabAppearance.FlatButtons;
        _steps.SizeMode = TabSizeMode.Fixed;
        _steps.ItemSize = new Size(174, 34);
        _steps.SelectedIndexChanged += (_, _) => UpdateNavigation();

        _steps.TabPages.Add(BuildOriginPage());
        _steps.TabPages.Add(BuildMappingPage());
        _steps.TabPages.Add(BuildPreviewPage());
        _steps.TabPages.Add(BuildEventsPage());
        _steps.TabPages.Add(BuildNotificationPage());
        _steps.TabPages.Add(BuildReviewPage());
        return _steps;
    }

    private TabPage BuildOriginPage()
    {
        var page = Page("1. Origem");
        var fields = FormTable();
        AddField(fields, "Nome do monitoramento", _name);

        var fileButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var browse = Button("Procurar...");
        browse.Click += (_, _) => BrowseFile();
        var analyze = Button("Analisar agora");
        analyze.Click += async (_, _) => await AnalyzeStructureAsync();
        fileButtons.Controls.Add(browse);
        fileButtons.Controls.Add(analyze);
        AddField(fields, "Arquivo Excel", _filePath, fileButtons);
        AddField(fields, "Seleção de abas", _worksheetSelection);
        AddField(fields, "Aba específica", _worksheet);
        AddField(fields, "Intervalo em minutos", _intervalMinutes);

        var note = InformationBox(
            "O arquivo é lido em modo somente leitura. O assistente cria uma única automação capaz de acompanhar todos os registros reconhecidos na área configurada.");

        var layout = VerticalLayout();
        layout.Controls.Add(fields);
        layout.Controls.Add(note);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildMappingPage()
    {
        var page = Page("2. Estrutura");
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 540, FixedPanel = FixedPanel.Panel1 };

        var columns = FormTable(180);
        AddField(columns, "Linha do cabeçalho", _headerRow);
        AddField(columns, "Primeira linha de dados", _dataStartRow);
        AddField(columns, "Última linha de dados", _dataEndRow);
        AddField(columns, "Coluna de numeração", _numberColumn);
        AddField(columns, $"Coluna de {_profile.CategoryName.ToLowerInvariant()}", _categoryColumn);
        AddField(columns, $"Coluna de {_profile.EntitySingular.ToLowerInvariant()}", _entityColumn);
        AddField(columns, $"Coluna de {_profile.CodeName.ToLowerInvariant()}", _codeColumn);
        AddField(columns, $"Coluna de {_profile.OwnerName.ToLowerInvariant()}", _ownerColumn);
        AddField(columns, "Primeira coluna monitorada", _firstValueColumn);
        AddField(columns, "Última coluna monitorada", _lastValueColumn);

        var detection = FormTable(210);
        AddField(detection, "Marcador do cabeçalho", _headerMarker);
        AddField(detection, "Texto esperado no cabeçalho", _headerTextContains);
        AddField(detection, "Rótulos de períodos/etapas", _periodLabels);
        AddField(detection, "Prefixos de seção", _sectionPrefixes);
        AddField(detection, "Prefixos a remover", _sectionPrefixesToRemove);
        AddField(detection, "Seções independentes", _standaloneSections);
        AddField(detection, "Seções sem períodos", _sectionsWithoutPeriods);
        AddField(detection, "Períodos fora do valor atual", _excludedCurrentPeriods);
        AddField(detection, "Cálculo do valor atual", _currentValueMode);

        split.Panel1.Controls.Add(Scrollable(columns));
        split.Panel2.Controls.Add(Scrollable(detection));
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildPreviewPage()
    {
        var page = Page("3. Análise e área");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        toolbar.Controls.Add(new Label { Text = "Aba:", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        _previewWorksheet.Width = 220;
        toolbar.Controls.Add(_previewWorksheet);
        var analyze = Button("Reanalisar");
        analyze.Click += async (_, _) => await AnalyzeStructureAsync();
        toolbar.Controls.Add(analyze);
        AddMappingButton(toolbar, "Usar seleção como linhas de dados", ApplySelectedRows);
        AddMappingButton(toolbar, "Usar seleção como colunas monitoradas", ApplySelectedValueColumns);
        AddMappingButton(toolbar, "Coluna: registro", () => ApplyCurrentColumn(_entityColumn, "registro"));
        AddMappingButton(toolbar, "Coluna: código", () => ApplyCurrentColumn(_codeColumn, "código"));
        AddMappingButton(toolbar, "Coluna: responsável", () => ApplyCurrentColumn(_ownerColumn, "responsável"));
        AddMappingButton(toolbar, "Coluna: grupo", () => ApplyCurrentColumn(_categoryColumn, "grupo"));
        AddMappingButton(toolbar, "Linha: cabeçalho", ApplyCurrentHeaderRow);

        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_analysisSummary, 0, 1);
        layout.Controls.Add(_preview, 0, 2);
        layout.Controls.Add(_warnings, 0, 3);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildEventsPage()
    {
        var page = Page("4. Eventos");
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 560 };

        var eventsGroup = Group("Mudanças que geram notificações");
        var events = CheckList(_valueChanges, _currentValueChanges, _ownerChanges, _monitorFormatting, _countChanges, _trackBlankCells);
        eventsGroup.Controls.Add(events);

        var aggregateGroup = Group("Indicadores agregados");
        var aggregates = CheckList(_aggregateGlobal, _aggregateByCategory, _aggregateByOwner, _includeBlankAggregates);
        aggregates.Controls.Add(InformationBox(
            "Indicadores agregados podem gerar várias mudanças derivadas de uma única edição. Eles ficam desativados por padrão para evitar excesso de mensagens."));
        aggregateGroup.Controls.Add(aggregates);

        split.Panel1.Controls.Add(eventsGroup);
        split.Panel2.Controls.Add(aggregateGroup);
        page.Controls.Add(split);
        return page;
    }

    private TabPage BuildNotificationPage()
    {
        var page = Page("5. Notificações");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(InformationBox(
            "O padrão é uma mensagem por alteração. Em canais externos, você também pode agrupar alterações por registro ou enviar um único resumo. As notificações do Windows permanecem sempre individuais."), 0, 0);
        layout.Controls.Add(_channelGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildReviewPage()
    {
        var page = Page("6. Revisão");
        _review.Dock = DockStyle.Fill;
        _review.Multiline = true;
        _review.ReadOnly = true;
        _review.ScrollBars = ScrollBars.Vertical;
        _review.Font = new Font("Consolas", 10F);
        _review.BackColor = SystemColors.Window;
        page.Controls.Add(_review);
        return page;
    }

    private Control BuildFooter()
    {
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 12, 0, 0) };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _footerStatus.AutoSize = true;
        _footerStatus.Anchor = AnchorStyles.Left;
        _footerStatus.ForeColor = Color.DimGray;

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var cancel = Button("Cancelar");
        cancel.DialogResult = DialogResult.Cancel;
        _finish.Text = "Criar monitoramento";
        _finish.AutoSize = true;
        _finish.Click += async (_, _) => await CreateDefinitionAsync();
        _next.Text = "Próximo";
        _next.AutoSize = true;
        _next.Click += async (_, _) => await MoveNextAsync();
        _back.Text = "Voltar";
        _back.AutoSize = true;
        _back.Click += (_, _) => { if (_steps.SelectedIndex > 0) _steps.SelectedIndex--; };

        buttons.Controls.Add(_finish);
        buttons.Controls.Add(_next);
        buttons.Controls.Add(_back);
        buttons.Controls.Add(cancel);
        footer.Controls.Add(_footerStatus, 0, 0);
        footer.Controls.Add(buttons, 1, 0);
        CancelButton = cancel;
        return footer;
    }

    private async Task MoveNextAsync()
    {
        if (!ValidateStep(_steps.SelectedIndex))
        {
            return;
        }

        if (_steps.SelectedIndex == 1 && _analysis is null)
        {
            await AnalyzeStructureAsync();
            if (_analysis is null)
            {
                return;
            }
        }

        if (_steps.SelectedIndex < _steps.TabPages.Count - 1)
        {
            _steps.SelectedIndex++;
        }
    }

    private void UpdateNavigation()
    {
        _back.Enabled = _steps.SelectedIndex > 0;
        _next.Visible = _steps.SelectedIndex < _steps.TabPages.Count - 1;
        _finish.Visible = _steps.SelectedIndex == _steps.TabPages.Count - 1;
        _footerStatus.Text = $"Etapa {_steps.SelectedIndex + 1} de {_steps.TabPages.Count}";
        if (_steps.SelectedIndex == _steps.TabPages.Count - 1)
        {
            UpdateReview();
        }
    }

    private bool ValidateStep(int step)
    {
        try
        {
            if (step >= 0)
            {
                if (string.IsNullOrWhiteSpace(_name.Text))
                {
                    throw new InvalidOperationException("Informe o nome do monitoramento.");
                }
                if (string.IsNullOrWhiteSpace(_filePath.Text) || !File.Exists(_filePath.Text.Trim()))
                {
                    throw new InvalidOperationException("Selecione uma planilha Excel existente.");
                }
            }

            if (step >= 1)
            {
                if (_firstValueColumn.Value > _lastValueColumn.Value)
                {
                    throw new InvalidOperationException("A primeira coluna monitorada não pode ser posterior à última coluna.");
                }
                if (_dataStartRow.Value > 0 && _dataEndRow.Value > 0 && _dataStartRow.Value > _dataEndRow.Value)
                {
                    throw new InvalidOperationException("A primeira linha de dados não pode ser posterior à última linha.");
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Assistente de planilhas");
            return false;
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
            _analysis = null;
            _analysisSummary.Text = "Arquivo selecionado. Execute a análise para visualizar e mapear a estrutura.";
        }
    }

    private async Task AnalyzeStructureAsync()
    {
        if (!ValidateStep(1))
        {
            return;
        }

        try
        {
            UseWaitCursor = true;
            _analysisSummary.Text = "Analisando estrutura, formatação e registros reconhecidos...";
            _analysis = await _monitoringService.AnalyzeAsync(BuildSource(), CancellationToken.None);
            var worksheetNames = _analysis.Visuals.Select(x => x.Worksheet).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _previewWorksheet.Items.Clear();
            _previewWorksheet.Items.AddRange(worksheetNames.Cast<object>().ToArray());
            if (_previewWorksheet.Items.Count > 0)
            {
                _previewWorksheet.SelectedIndex = 0;
            }

            _warnings.Items.Clear();
            foreach (var warning in _analysis.Warnings)
            {
                _warnings.Items.Add(warning);
            }
            if (_analysis.Warnings.Count == 0)
            {
                _warnings.Items.Add("Nenhum aviso estrutural foi encontrado.");
            }

            _analysisSummary.Text =
                $"Reconhecidos: {_analysis.EntityCount:N0} {_entityPlural.Text.Trim().ToLowerInvariant()}, " +
                $"{_analysis.SectionCount:N0} {_categoryName.Text.Trim().ToLowerInvariant()}(s), " +
                $"{_analysis.StatusCellCount:N0} células monitoráveis, {_analysis.HighlightedCellCount:N0} destaques e " +
                $"{_analysis.Visuals.Count:N0} aba(s).";
        }
        catch (Exception exception)
        {
            _analysis = null;
            _analysisSummary.Text = "Não foi possível reconhecer a estrutura com o mapeamento atual.";
            VisualEditorSupport.ShowError(this, exception, "Análise da planilha");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderSelectedWorksheet()
    {
        if (_analysis is null || _previewWorksheet.SelectedItem is not string worksheetName)
        {
            return;
        }

        var visual = _analysis.Visuals.FirstOrDefault(x => string.Equals(x.Worksheet, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (visual is null)
        {
            return;
        }

        _preview.SuspendLayout();
        try
        {
            _preview.Rows.Clear();
            _preview.Columns.Clear();
            for (var column = 1; column <= visual.ColumnCount; column++)
            {
                _preview.Columns.Add($"C{column}", ColumnName(column));
                _preview.Columns[column - 1].Width = Math.Clamp((int)(visual.ColumnWidths.GetValueOrDefault(column, 12D) * 7D), 65, 260);
                _preview.Columns[column - 1].SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            _preview.Rows.Add(visual.RowCount);
            for (var row = 1; row <= visual.RowCount; row++)
            {
                _preview.Rows[row - 1].HeaderCell.Value = row.ToString();
                _preview.Rows[row - 1].Height = Math.Clamp((int)visual.RowHeights.GetValueOrDefault(row, 15D) + 8, 24, 80);
            }

            foreach (var cell in visual.Cells)
            {
                if (cell.Row < 1 || cell.Column < 1 || cell.Row > _preview.Rows.Count || cell.Column > _preview.Columns.Count)
                {
                    continue;
                }
                var target = _preview.Rows[cell.Row - 1].Cells[cell.Column - 1];
                target.Value = cell.Value;
                target.ToolTipText = $"{cell.Address} — linha {cell.Row}, coluna {cell.Column}";
                target.Style.Font = new Font(_preview.Font, (cell.Bold ? FontStyle.Bold : FontStyle.Regular) | (cell.Italic ? FontStyle.Italic : FontStyle.Regular));
                if (!string.IsNullOrWhiteSpace(cell.FillColor) && TryColor(cell.FillColor, out var color))
                {
                    target.Style.BackColor = color;
                    target.Style.ForeColor = GetContrastColor(color);
                }
            }
        }
        finally
        {
            _preview.ResumeLayout();
        }
    }

    private void ApplySelectedRows()
    {
        if (_preview.SelectedCells.Count == 0)
        {
            ShowSelectionMessage();
            return;
        }
        var rows = _preview.SelectedCells.Cast<DataGridViewCell>().Select(x => x.RowIndex + 1).ToArray();
        SetNumber(_dataStartRow, rows.Min());
        SetNumber(_dataEndRow, rows.Max());
        _footerStatus.Text = $"Linhas de dados definidas: {rows.Min()} a {rows.Max()}.";
    }

    private void ApplySelectedValueColumns()
    {
        if (_preview.SelectedCells.Count == 0)
        {
            ShowSelectionMessage();
            return;
        }
        var columns = _preview.SelectedCells.Cast<DataGridViewCell>().Select(x => x.ColumnIndex + 1).ToArray();
        SetNumber(_firstValueColumn, columns.Min());
        SetNumber(_lastValueColumn, columns.Max());
        _footerStatus.Text = $"Colunas monitoradas definidas: {ColumnName(columns.Min())} a {ColumnName(columns.Max())}.";
    }

    private void ApplyCurrentColumn(NumericUpDown target, string role)
    {
        if (_preview.CurrentCell is null)
        {
            ShowSelectionMessage();
            return;
        }
        var column = _preview.CurrentCell.ColumnIndex + 1;
        SetNumber(target, column);
        _footerStatus.Text = $"A coluna {ColumnName(column)} foi definida como {role}.";
    }

    private void ApplyCurrentHeaderRow()
    {
        if (_preview.CurrentCell is null)
        {
            ShowSelectionMessage();
            return;
        }
        var row = _preview.CurrentCell.RowIndex + 1;
        SetNumber(_headerRow, row);
        _footerStatus.Text = $"A linha {row} foi definida como cabeçalho.";
    }

    private void UpdateAggregateOptions()
    {
        foreach (var control in new Control[] { _aggregateGlobal, _aggregateByCategory, _aggregateByOwner, _includeBlankAggregates })
        {
            control.Enabled = _countChanges.Checked;
        }
    }

    private async Task CreateDefinitionAsync()
    {
        if (!ValidateStep(5))
        {
            return;
        }

        try
        {
            if (_analysis is null)
            {
                await AnalyzeStructureAsync();
                if (_analysis is null)
                {
                    return;
                }
            }

            var selectedChannels = ReadSelectedChannels();
            var source = BuildSource();
            var actions = BuildActions(selectedChannels);
            Definition = new AutomationDefinition
            {
                Id = _automationId,
                Name = _name.Text.Trim(),
                Description = $"Monitoramento de planilha criado pelo assistente '{_profile.DisplayName}', com mapeamento visual e políticas de notificação por canal.",
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

    private List<SelectedChannel> ReadSelectedChannels()
    {
        var selected = new List<SelectedChannel>();
        foreach (DataGridViewRow row in _channelGrid.Rows)
        {
            if (row.Tag is not ChannelConfiguration channel || Convert.ToBoolean(row.Cells["Use"].Value) != true)
            {
                continue;
            }

            var recipients = _channelRecipients.GetValueOrDefault(channel.Id, []);
            if (channel.Type != ChannelType.LocalWindows && recipients.Count == 0)
            {
                throw new InvalidOperationException($"Selecione ao menos um contato, grupo ou destinatário manual para o canal '{channel.Name}'.");
            }

            var modeText = Convert.ToString(row.Cells["DeliveryMode"].Value) ?? DeliveryIndividual;
            var grouping = channel.Type == ChannelType.LocalWindows
                ? NotificationGroupingMode.Individual
                : modeText switch
                {
                    DeliveryByEntity => NotificationGroupingMode.ByEntity,
                    DeliverySingleMessage => NotificationGroupingMode.SingleMessage,
                    _ => NotificationGroupingMode.Individual
                };
            selected.Add(new SelectedChannel(channel, recipients, grouping));
        }
        return selected;
    }

    private List<ActionDefinition> BuildActions(IReadOnlyCollection<SelectedChannel> channels)
    {
        var actions = new List<ActionDefinition>();
        if (channels.Count == 0)
        {
            return actions;
        }

        var entity = _entitySingular.Text.Trim();
        var category = _categoryName.Text.Trim();
        var owner = _ownerName.Text.Trim();
        var period = _periodName.Text.Trim();
        var value = _valueName.Text.Trim();
        var code = _codeName.Text.Trim();

        if (_valueChanges.Checked)
        {
            actions.Add(BuildAction(
                $"Mudança de {value.ToLowerInvariant()} por {period.ToLowerInvariant()}",
                channels,
                [Equal("__recordType", "Status"), Changed("Status")],
                $"{value} alterada — {{{{Entity}}}} / {{{{Period}}}}",
                $"{entity} {{{{Entity}}}} ({code.ToLowerInvariant()} {{{{Code}}}}), {category.ToLowerInvariant()} {{{{Category}}}}, {period.ToLowerInvariant()} {{{{Period}}}}: '{{{{previous.Status}}}}' → '{{{{Status}}}}'. Célula {{{{CellAddress}}}}. {owner}: {{{{Owner}}}}."));
        }

        if (_currentValueChanges.Checked)
        {
            actions.Add(BuildAction(
                $"Mudança do {value.ToLowerInvariant()} atual",
                channels,
                [Equal("__recordType", "Entity"), Changed("CurrentValue")],
                $"{value} atual alterada — {{{{Entity}}}}",
                $"{entity} {{{{Entity}}}} ({code.ToLowerInvariant()} {{{{Code}}}}), {category.ToLowerInvariant()} {{{{Category}}}}: {value.ToLowerInvariant()} atual '{{{{previous.CurrentValue}}}}' → '{{{{CurrentValue}}}}'. {period}: {{{{CurrentPeriod}}}}. {owner}: {{{{Owner}}}}."));
        }

        if (_ownerChanges.Checked)
        {
            actions.Add(BuildAction(
                $"Mudança de {owner.ToLowerInvariant()}",
                channels,
                [Equal("__recordType", "Entity"), Changed("Owner")],
                $"{owner} alterado — {{{{Entity}}}}",
                $"{entity} {{{{Entity}}}} ({code.ToLowerInvariant()} {{{{Code}}}}), {category.ToLowerInvariant()} {{{{Category}}}}: {owner.ToLowerInvariant()} '{{{{previous.Owner}}}}' → '{{{{Owner}}}}'."));
        }

        if (_countChanges.Checked)
        {
            actions.Add(BuildAction(
                "Mudança de indicador agregado",
                channels,
                [Equal("__recordType", "Aggregate"), Changed("Count")],
                "Indicador alterado — {{MetricDisplay}}",
                "{{MetricDisplay}}; agrupamento {{ScopeDisplay}} / {{Group}}; {{Period}}; valor {{StatusDisplay}}: {{previous.Count}} → {{Count}} {{Unit}}."));
        }

        if (_monitorFormatting.Checked)
        {
            var action = BuildAction(
                "Mudança de formatação",
                channels,
                [Equal("__recordType", "Status")],
                "Formatação alterada — {{Entity}} / {{Period}}",
                $"{entity} {{{{Entity}}}}, {period.ToLowerInvariant()} {{{{Period}}}}: cor '{{{{previous.FillColor}}}}' → '{{{{FillColor}}}}'; destaque '{{{{previous.IsHighlighted}}}}' → '{{{{IsHighlighted}}}}'. Célula {{{{CellAddress}}}}.");
            action.Conditions!.Root!.Groups.Add(new RuleGroupDefinition
            {
                Operator = LogicalOperator.Or,
                Rules = [Changed("FillColor"), Changed("IsHighlighted")]
            });
            actions.Add(action);
        }

        return actions;
    }

    private DataSourceDefinition BuildSource()
    {
        var calendar = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["JAN"] = 1, ["FEV"] = 2, ["MAR"] = 3, ["ABR"] = 4,
            ["MAI"] = 5, ["JUN"] = 6, ["JUL"] = 7, ["AGO"] = 8,
            ["SET"] = 9, ["OUT"] = 10, ["NOV"] = 11, ["DEZ"] = 12
        };

        var settings = new
        {
            filePath = _filePath.Text.Trim(),
            worksheet = _worksheetSelection.SelectedIndex == 1 && !string.IsNullOrWhiteSpace(_worksheet.Text) ? _worksheet.Text.Trim() : null,
            headerRow = (int)_headerRow.Value,
            ignoreEmptyRows = true,
            mode = "SectionedMatrix",
            profileName = _profile.DisplayName,
            worksheetSelection = _worksheetSelection.SelectedIndex switch
            {
                0 => "LatestYear",
                2 => "AllMatching",
                _ => "Fixed"
            },
            worksheetPattern = @"(?<year>20\d{2})",
            matrix = new
            {
                headerMarker = _headerMarker.Text.Trim(),
                headerTextContains = _headerTextContains.Text.Trim(),
                periodLabels = _periodLabels.Text.Trim(),
                sectionTitlePrefixes = _sectionPrefixes.Text.Trim(),
                sectionNamePrefixesToRemove = _sectionPrefixesToRemove.Text.Trim(),
                standaloneSectionTitles = _standaloneSections.Text.Trim(),
                sectionsWithoutPeriods = _sectionsWithoutPeriods.Text.Trim(),
                currentStatusExcludedPeriods = _excludedCurrentPeriods.Text.Trim(),
                currentStatusMode = _currentValueMode.SelectedIndex == 1 ? "CalendarPeriod" : "LastFilled",
                calendarPeriodNumbers = calendar,
                numberColumn = (int)_numberColumn.Value,
                sectionColumn = (int)_categoryColumn.Value,
                companyColumn = (int)_entityColumn.Value,
                codeColumn = (int)_codeColumn.Value,
                collaboratorColumn = (int)_ownerColumn.Value,
                firstPeriodColumn = (int)_firstValueColumn.Value,
                lastPeriodColumn = (int)_lastValueColumn.Value,
                dataStartRow = (int)_dataStartRow.Value,
                dataEndRow = (int)_dataEndRow.Value,
                includeBlankStatuses = _trackBlankCells.Checked,
                includeBlankValuesInAggregates = _includeBlankAggregates.Checked,
                includeFormatting = _monitorFormatting.Checked,
                generateCompanyRecords = _currentValueChanges.Checked || _ownerChanges.Checked || _countChanges.Checked,
                generateAggregateRecords = _countChanges.Checked,
                aggregateGlobal = _aggregateGlobal.Checked,
                aggregateBySection = _aggregateByCategory.Checked,
                aggregateByCollaborator = _aggregateByOwner.Checked,
                autoDetectStandaloneSections = true,
                entitySingularName = UseLabel(_entitySingular.Text, "Registro"),
                entityPluralName = UseLabel(_entityPlural.Text, "Registros"),
                ownerName = UseLabel(_ownerName.Text, "Responsável"),
                categoryName = UseLabel(_categoryName.Text, "Grupo"),
                periodName = UseLabel(_periodName.Text, "Período"),
                codeName = UseLabel(_codeName.Text, "Código"),
                valueName = UseLabel(_valueName.Text, "Valor"),
                statusLabels = new Dictionary<string, string>()
            },
            designerFields = new[]
            {
                "__recordType", "Worksheet", "Year", "Section", "Regime", "EntityKey", "Entity", "CompanyKey", "Company",
                "Code", "Owner", "Collaborator", "Category", "Period", "PeriodBase", "Status", "StatusDisplay", "StatusMeaning",
                "Value", "ValueDisplay", "CurrentPeriod", "CurrentStatus", "CurrentStatusDisplay", "CurrentValue", "CurrentValueDisplay",
                "Metric", "MetricDisplay", "Unit", "Count", "Scope", "ScopeDisplay", "Group", "CellAddress", "FillColor", "IsHighlighted"
            }
        };
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(settings, FlowJson.Options));
        return new DataSourceDefinition
        {
            Id = Guid.NewGuid(),
            Alias = "planilha",
            Name = _profile.DisplayName,
            Type = SourceType.Excel,
            IsPrimary = true,
            Enabled = true,
            KeyFields = ["__recordKey"],
            Configuration = document.RootElement.Clone()
        };
    }

    private static ActionDefinition BuildAction(
        string name,
        IReadOnlyCollection<SelectedChannel> channels,
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
                Required = true,
                GroupingMode = x.GroupingMode,
                GroupField = "EntityKey",
                GroupingWindowSeconds = x.GroupingMode == NotificationGroupingMode.Individual ? 0 : 8
            }).ToList()
        };

        action.Recipients = channels
            .Where(x => x.Configuration.Type != ChannelType.LocalWindows)
            .SelectMany(x => x.Recipients)
            .GroupBy(x => $"{x.Type}:{x.ChannelType}:{x.Value}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new RecipientDefinition
            {
                Type = group.First().Type,
                Value = group.First().Value,
                ChannelType = group.First().ChannelType,
                DisplayName = group.First().DisplayName
            })
            .ToList();
        return action;
    }

    private void UpdateReview()
    {
        var channels = _channelGrid.Rows.Cast<DataGridViewRow>()
            .Where(x => Convert.ToBoolean(x.Cells["Use"].Value) == true)
            .Select(x => $"- {x.Cells["Name"].Value}: {x.Cells["Recipients"].Value}; {x.Cells["DeliveryMode"].Value}")
            .ToArray();
        var events = new[]
        {
            _valueChanges.Checked ? "- Mudanças por célula" : null,
            _currentValueChanges.Checked ? "- Mudança do valor atual" : null,
            _ownerChanges.Checked ? "- Mudança de responsável" : null,
            _monitorFormatting.Checked ? "- Mudança de formatação" : null,
            _countChanges.Checked ? "- Indicadores agregados" : null
        }.Where(x => x is not null);

        _review.Text =
            $"MODELO\r\n{_profile.DisplayName}\r\n\r\n" +
            $"ORIGEM\r\n{_filePath.Text.Trim()}\r\nIntervalo: {_intervalMinutes.Value} minuto(s)\r\n\r\n" +
            $"MAPEAMENTO\r\nCabeçalho: linha {_headerRow.Value}\r\nLinhas de dados: {FormatRange(_dataStartRow.Value, _dataEndRow.Value)}\r\n" +
            $"Registro: coluna {_entityColumn.Value}; Código: {_codeColumn.Value}; Responsável: {_ownerColumn.Value}; Grupo: {_categoryColumn.Value}\r\n" +
            $"Valores: colunas {_firstValueColumn.Value} a {_lastValueColumn.Value}\r\n\r\n" +
            $"EVENTOS\r\n{string.Join("\r\n", events)}\r\n\r\n" +
            $"CANAIS\r\n{(channels.Length == 0 ? "- Nenhum disparo externo; apenas acompanhamento administrativo" : string.Join("\r\n", channels))}\r\n\r\n" +
            $"ANÁLISE\r\n{(_analysis is null ? "A estrutura será analisada antes da criação." : _analysisSummary.Text)}";
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

    private static NumericUpDown Number(int minimum, int maximum, int value) => new()
    {
        Minimum = minimum,
        Maximum = maximum,
        Value = value,
        ThousandsSeparator = true,
        Width = 120
    };

    private static void SetNumber(NumericUpDown control, int value) =>
        control.Value = Math.Clamp(value, (int)control.Minimum, (int)control.Maximum);

    private static TabPage Page(string title) => new(title) { Padding = new Padding(4), UseVisualStyleBackColor = true };

    private static TableLayoutPanel FormTable(int labelWidth = 210)
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(12) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return table;
    }

    private static void AddField(TableLayoutPanel table, string label, Control control, Control? extra = null)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(VisualEditorSupport.LabelFor(label), 0, row);
        control.Dock = DockStyle.Fill;
        table.Controls.Add(control, 1, row);
        if (extra is not null)
        {
            table.Controls.Add(extra, 2, row);
        }
    }

    private static FlowLayoutPanel VerticalLayout() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        Padding = new Padding(8)
    };

    private static Control Scrollable(Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        panel.Controls.Add(content);
        return panel;
    }

    private static GroupBox Group(string title) => new() { Text = title, Dock = DockStyle.Fill, Padding = new Padding(12) };

    private static FlowLayoutPanel CheckList(params CheckBox[] checkBoxes)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        foreach (var checkBox in checkBoxes)
        {
            checkBox.AutoSize = true;
            checkBox.Margin = new Padding(6, 8, 6, 4);
            flow.Controls.Add(checkBox);
        }
        return flow;
    }

    private static Label InformationBox(string text) => new()
    {
        AutoSize = true,
        MaximumSize = new Size(1040, 0),
        Text = text,
        ForeColor = Color.FromArgb(55, 70, 90),
        BackColor = Color.FromArgb(240, 245, 250),
        Padding = new Padding(12),
        Margin = new Padding(4, 8, 4, 8)
    };

    private static Button Button(string text) => new() { Text = text, AutoSize = true, Height = 31, Margin = new Padding(3) };

    private static void AddMappingButton(Control parent, string text, Action action)
    {
        var button = Button(text);
        button.Click += (_, _) => action();
        parent.Controls.Add(button);
    }

    private void ShowSelectionMessage() => MessageBox.Show(
        this,
        "Selecione uma ou mais células na prévia da planilha.",
        "Seleção de área",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);

    private static string ColumnName(int column)
    {
        var result = string.Empty;
        while (column > 0)
        {
            column--;
            result = (char)('A' + column % 26) + result;
            column /= 26;
        }
        return result;
    }

    private static bool TryColor(string value, out Color color)
    {
        try
        {
            color = ColorTranslator.FromHtml(value.StartsWith('#') ? value : $"#{value}");
            return true;
        }
        catch
        {
            color = Color.Empty;
            return false;
        }
    }

    private static Color GetContrastColor(Color color) =>
        color.R * 0.299 + color.G * 0.587 + color.B * 0.114 > 160 ? Color.Black : Color.White;

    private static string UseLabel(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string FormatRange(decimal start, decimal end) =>
        start == 0 && end == 0 ? "automáticas" : $"{start} a {(end == 0 ? "fim" : end)}";

    private sealed record SelectedChannel(
        ChannelConfiguration Configuration,
        IReadOnlyList<RecipientDefinition> Recipients,
        NotificationGroupingMode GroupingMode);
}
