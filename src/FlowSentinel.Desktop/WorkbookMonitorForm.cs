using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Desktop;

internal sealed class WorkbookMonitorForm : Form
{
    private readonly IFlowStore _store;
    private readonly IWorkbookMonitoringService _monitoringService;
    private readonly ComboBox _sourceSelector = new();
    private readonly ComboBox _worksheetSelector = new();
    private readonly Label _kpis = new();
    private readonly Label _status = new();
    private readonly DataGridView _visualGrid = new();
    private readonly DataGridView _summaryGrid = CreateReadOnlyGrid();
    private readonly DataGridView _recordsGrid = CreateReadOnlyGrid();
    private readonly DataGridView _changesGrid = CreateReadOnlyGrid();
    private readonly ListBox _warnings = new();
    private readonly ComboBox _recordTypeFilter = new();
    private readonly ComboBox _sectionFilter = new();
    private readonly ComboBox _collaboratorFilter = new();
    private readonly ComboBox _periodFilter = new();
    private readonly ComboBox _statusFilter = new();
    private readonly TextBox _companyFilter = new();
    private readonly Dictionary<FontStyle, Font> _cellFonts = [];

    private WorkbookMonitoringAnalysis? _analysis;
    private MonitoringSourceItem? _selectedSource;

    internal WorkbookMonitorForm(IFlowStore store, IWorkbookMonitoringService monitoringService)
    {
        _store = store;
        _monitoringService = monitoringService;
        Text = "Painel administrativo de planilhas";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(1380, 820);
        MinimumSize = new Size(1050, 650);
        Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath) ?? SystemIcons.Application;
        BuildLayout();
        Shown += async (_, _) => await LoadSourcesAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        toolbar.Controls.Add(new Label { Text = "Monitoramento:", AutoSize = true, Margin = new Padding(4, 9, 4, 0) });
        _sourceSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _sourceSelector.Width = 370;
        _sourceSelector.SelectedIndexChanged += (_, _) => _selectedSource = _sourceSelector.SelectedItem as MonitoringSourceItem;
        toolbar.Controls.Add(_sourceSelector);
        AddButton(toolbar, "Analisar agora", async (_, _) => await AnalyzeAsync());
        AddButton(toolbar, "Comparar alterações", async (_, _) => await CompareAsync());
        AddButton(toolbar, "Gravar linha de base", async (_, _) => await SaveBaselineAsync());
        AddButton(toolbar, "Legenda de valores", async (_, _) => await EditStatusDictionaryAsync());
        AddButton(toolbar, "Abrir planilha", (_, _) => OpenWorkbook());
        AddButton(toolbar, "Fechar", (_, _) => Close());
        root.Controls.Add(toolbar, 0, 0);

        _kpis.Dock = DockStyle.Fill;
        _kpis.AutoSize = true;
        _kpis.Font = new Font(Font, FontStyle.Bold);
        _kpis.Padding = new Padding(4, 8, 4, 8);
        _kpis.Text = "Selecione um monitoramento e clique em Analisar agora.";
        root.Controls.Add(_kpis, 0, 1);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildVisualTab());
        tabs.TabPages.Add(BuildSummaryTab());
        tabs.TabPages.Add(BuildRecordsTab());
        tabs.TabPages.Add(BuildChangesTab());
        tabs.TabPages.Add(BuildWarningsTab());
        root.Controls.Add(tabs, 0, 2);

        _status.Dock = DockStyle.Fill;
        _status.AutoSize = true;
        _status.Padding = new Padding(4, 6, 4, 0);
        _status.Text = "Pronto";
        root.Controls.Add(_status, 0, 3);
        Controls.Add(root);
    }

    private TabPage BuildVisualTab()
    {
        var page = new TabPage("Planilha organizada");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        header.Controls.Add(new Label { Text = "Aba:", AutoSize = true, Margin = new Padding(4, 8, 4, 0) });
        _worksheetSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _worksheetSelector.Width = 250;
        _worksheetSelector.SelectedIndexChanged += (_, _) => ShowSelectedWorksheet();
        header.Controls.Add(_worksheetSelector);
        header.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(16, 8, 4, 0),
            Text = "A visualização preserva a posição, os valores e os destaques da aba original."
        });
        layout.Controls.Add(header, 0, 0);

        ConfigureVisualGrid();
        layout.Controls.Add(_visualGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildSummaryTab()
    {
        var page = new TabPage("Quantidades por valor");
        _summaryGrid.Dock = DockStyle.Fill;
        _summaryGrid.AutoGenerateColumns = false;
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Metric), "Indicador", 105));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Scope), "Escopo", 70));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Group), "Grupo", 120));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Worksheet), "Aba", 90));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Period), "Período", 60));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Status), "Valor", 55));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Meaning), "Significado", 135));
        _summaryGrid.Columns.Add(TextColumn(nameof(SummaryRow.Count), "Quantidade", 60));
        page.Controls.Add(_summaryGrid);
        return page;
    }

    private TabPage BuildRecordsTab()
    {
        var page = new TabPage("Entidades e registros");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        ConfigureFilter(_recordTypeFilter, 125);
        ConfigureFilter(_sectionFilter, 160);
        ConfigureFilter(_collaboratorFilter, 150);
        ConfigureFilter(_periodFilter, 100);
        ConfigureFilter(_statusFilter, 100);
        _companyFilter.Width = 190;
        _companyFilter.PlaceholderText = "Entidade ou código";
        foreach (var control in new Control[]
                 {
                     Labeled("Tipo", _recordTypeFilter), Labeled("Seção", _sectionFilter),
                     Labeled("Responsável", _collaboratorFilter), Labeled("Período", _periodFilter),
                     Labeled("Valor", _statusFilter), Labeled("Pesquisar", _companyFilter)
                 })
        {
            filters.Controls.Add(control);
        }
        AddButton(filters, "Aplicar filtros", (_, _) => ApplyRecordFilters());
        AddButton(filters, "Limpar", (_, _) => ClearRecordFilters());
        layout.Controls.Add(filters, 0, 0);

        _recordsGrid.Dock = DockStyle.Fill;
        _recordsGrid.AutoGenerateColumns = false;
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Type), "Tipo", 55));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Worksheet), "Aba", 75));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Section), "Seção", 100));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Entity), "Entidade", 170));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Code), "Código", 65));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Owner), "Responsável", 85));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Period), "Período", 55));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Value), "Valor", 55));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Meaning), "Significado", 110));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.CurrentValue), "Valor atual", 70));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.Count), "Quantidade", 55));
        _recordsGrid.Columns.Add(TextColumn(nameof(RecordRow.CellAddress), "Célula", 45));
        layout.Controls.Add(_recordsGrid, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildChangesTab()
    {
        var page = new TabPage("O que mudou");
        _changesGrid.Dock = DockStyle.Fill;
        _changesGrid.AutoGenerateColumns = false;
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.ChangeType), "Mudança", 55));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.RecordType), "Tipo", 55));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.Worksheet), "Aba", 65));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.Section), "Seção", 95));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.Entity), "Entidade", 160));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.Code), "Código", 55));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.Period), "Período", 50));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.ChangedFields), "Campos alterados", 120));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.PreviousValue), "Antes", 70));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.CurrentValue), "Agora", 70));
        _changesGrid.Columns.Add(TextColumn(nameof(ChangeRow.CellAddress), "Célula", 45));
        page.Controls.Add(_changesGrid);
        return page;
    }

    private TabPage BuildWarningsTab()
    {
        var page = new TabPage("Avisos de leitura");
        _warnings.Dock = DockStyle.Fill;
        _warnings.HorizontalScrollbar = true;
        page.Controls.Add(_warnings);
        return page;
    }

    private async Task LoadSourcesAsync()
    {
        await RunBusyAsync("Carregando monitoramentos...", async () =>
        {
            var items = new List<MonitoringSourceItem>();
            var automations = await _store.GetAutomationsAsync(CancellationToken.None);
            foreach (var automation in automations.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var definition = await _store.GetAutomationDefinitionAsync(automation.Id, CancellationToken.None);
                if (definition is null)
                {
                    continue;
                }

                foreach (var source in definition.Sources.Where(IsAdministrativeExcelSource))
                {
                    items.Add(new MonitoringSourceItem(definition.Id, definition.Name, source));
                }
            }

            _sourceSelector.DataSource = null;
            _sourceSelector.Items.Clear();
            foreach (var item in items)
            {
                _sourceSelector.Items.Add(item);
            }
            if (_sourceSelector.Items.Count > 0)
            {
                _sourceSelector.SelectedIndex = 0;
                _selectedSource = _sourceSelector.SelectedItem as MonitoringSourceItem;
            }
            else
            {
                _selectedSource = null;
                _kpis.Text = "Nenhuma fonte Excel em modo matriz estruturada foi encontrada. Crie uma automação e configure uma fonte Excel nesse modo; o modelo RP-102 é apenas um exemplo opcional.";
            }
        });

        if (_selectedSource is not null)
        {
            await AnalyzeAsync();
        }
    }

    private async Task AnalyzeAsync()
    {
        if (_selectedSource is null)
        {
            MessageBox.Show(this, "Selecione um monitoramento de planilha.", "FlowSentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await RunBusyAsync("Lendo e organizando a planilha...", async () =>
        {
            var definition = await _store.GetAutomationDefinitionAsync(_selectedSource.AutomationId, CancellationToken.None)
                             ?? throw new InvalidOperationException("A automação selecionada não foi encontrada.");
            var source = definition.Sources.FirstOrDefault(x => x.Id == _selectedSource.SourceId)
                         ?? throw new InvalidOperationException("A fonte selecionada não foi encontrada.");
            _analysis = await _monitoringService.AnalyzeAsync(source, CancellationToken.None);
            _selectedSource = new MonitoringSourceItem(definition.Id, definition.Name, source);
            PopulateAnalysis();
        });
    }

    private void PopulateAnalysis()
    {
        if (_analysis is null)
        {
            return;
        }

        var labels = _analysis.Labels;
        ApplyConfiguredLabels(labels);
        _kpis.Text = $"{labels.EntityPlural}: {_analysis.EntityCount:N0}    |    {labels.Category}s: {_analysis.SectionCount:N0}    |    Valores monitorados: {_analysis.StatusCellCount:N0}    |    Vazios: {_analysis.BlankStatusCount:N0}    |    Destacados: {_analysis.HighlightedCellCount:N0}    |    Abas: {_analysis.Worksheets.Count:N0}";
        _worksheetSelector.Items.Clear();
        foreach (var visual in _analysis.Visuals)
        {
            _worksheetSelector.Items.Add(visual.Worksheet);
        }
        if (_worksheetSelector.Items.Count > 0)
        {
            _worksheetSelector.SelectedIndex = 0;
        }
        else
        {
            _visualGrid.Columns.Clear();
            _visualGrid.Rows.Clear();
        }

        _summaryGrid.DataSource = _analysis.StatusSummaries.Select(x => new SummaryRow
        {
            Metric = MetricText(x.Metric, labels),
            Scope = ScopeText(x.Scope, labels),
            Group = x.Group,
            Worksheet = x.Worksheet,
            Period = x.Period,
            Status = x.Status,
            Meaning = x.StatusMeaning,
            Count = x.Count
        }).ToList();

        PopulateRecordFilters();
        ApplyRecordFilters();
        _warnings.Items.Clear();
        if (_analysis.Warnings.Count == 0)
        {
            _warnings.Items.Add("Nenhum aviso de leitura.");
        }
        else
        {
            _warnings.Items.AddRange(_analysis.Warnings.Cast<object>().ToArray());
        }
        _changesGrid.DataSource = new List<ChangeRow>();
        _status.Text = $"Análise concluída em {_analysis.AnalyzedAt.LocalDateTime:dd/MM/yyyy HH:mm:ss}. Arquivo: {_analysis.FilePath}";
    }

    private async Task CompareAsync()
    {
        if (_analysis is null)
        {
            await AnalyzeAsync();
        }
        if (_analysis is null)
        {
            return;
        }

        await RunBusyAsync("Comparando com a linha de base...", async () =>
        {
            var result = await _monitoringService.CompareWithBaselineAsync(_analysis, CancellationToken.None);
            if (!result.BaselineExists)
            {
                _changesGrid.DataSource = new List<ChangeRow>();
                MessageBox.Show(
                    this,
                    "Ainda não existe uma linha de base. Analise a planilha e clique em 'Gravar linha de base'.",
                    "FlowSentinel",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            _changesGrid.DataSource = result.Changes.Select(x => MapChange(x, _analysis.Labels)).ToList();
            _status.Text = $"Comparação concluída: {result.Changes.Count:N0} alteração(ões). Linha de base de {result.BaselineCreatedAt?.LocalDateTime:dd/MM/yyyy HH:mm:ss}.";
        });
    }

    private async Task SaveBaselineAsync()
    {
        if (_analysis is null)
        {
            await AnalyzeAsync();
        }
        if (_analysis is null || MessageBox.Show(
                this,
                "Gravar o estado atual como referência? A próxima comparação mostrará apenas mudanças posteriores.",
                "Linha de base",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        await RunBusyAsync("Gravando linha de base...", async () =>
        {
            await _monitoringService.SaveBaselineAsync(_analysis, CancellationToken.None);
            _changesGrid.DataSource = new List<ChangeRow>();
            _status.Text = $"Linha de base gravada em {DateTime.Now:dd/MM/yyyy HH:mm:ss}.";
        });
    }

    private async Task EditStatusDictionaryAsync()
    {
        if (_selectedSource is null)
        {
            return;
        }

        var definition = await _store.GetAutomationDefinitionAsync(_selectedSource.AutomationId, CancellationToken.None)
                         ?? throw new InvalidOperationException("A automação selecionada não foi encontrada.");
        var source = definition.Sources.FirstOrDefault(x => x.Id == _selectedSource.SourceId)
                     ?? throw new InvalidOperationException("A fonte selecionada não foi encontrada.");
        var root = JsonNode.Parse(source.Configuration.GetRawText())?.AsObject()
                   ?? throw new InvalidOperationException("A configuração da fonte está inválida.");
        var matrix = root["matrix"] as JsonObject ?? new JsonObject();
        root["matrix"] = matrix;
        var labelsNode = matrix["statusLabels"] as JsonObject;
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (labelsNode is not null)
        {
            foreach (var item in labelsNode)
            {
                labels[item.Key] = item.Value?.GetValue<string>() ?? string.Empty;
            }
        }
        var discovered = _analysis?.Records
            .SelectMany(x => new[] { x.Value, x.CurrentValue, x.Status, x.CurrentStatus })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase) ?? Enumerable.Empty<string>();

        using var form = new StatusDictionaryForm(labels, discovered);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var updatedLabels = new JsonObject();
        foreach (var item in form.StatusLabels.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            updatedLabels[item.Key] = item.Value;
        }
        matrix["statusLabels"] = updatedLabels;
        source.Configuration = JsonSerializer.SerializeToElement(root, FlowJson.Options);
        await _store.SaveAutomationAsync(definition, CancellationToken.None);
        _selectedSource = new MonitoringSourceItem(definition.Id, definition.Name, source);
        await AnalyzeAsync();
    }

    private void ShowSelectedWorksheet()
    {
        var name = _worksheetSelector.SelectedItem as string;
        var visual = _analysis?.Visuals.FirstOrDefault(x => string.Equals(x.Worksheet, name, StringComparison.OrdinalIgnoreCase));
        if (visual is null)
        {
            return;
        }

        _visualGrid.SuspendLayout();
        try
        {
            _visualGrid.Columns.Clear();
            _visualGrid.Rows.Clear();
            for (var column = 1; column <= visual.ColumnCount; column++)
            {
                _visualGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = ExcelColumnName(column),
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    Width = visual.ColumnWidths.TryGetValue(column, out var excelWidth)
                        ? (int)Math.Clamp(excelWidth * 7d + 12d, 42d, 420d)
                        : column == 2 ? 240 : column is 3 or 4 ? 105 : 75
                });
            }
            if (visual.RowCount > 0)
            {
                _visualGrid.Rows.Add(visual.RowCount);
            }
            for (var row = 0; row < visual.RowCount; row++)
            {
                _visualGrid.Rows[row].HeaderCell.Value = (row + 1).ToString();
                if (visual.RowHeights.TryGetValue(row + 1, out var excelHeight))
                {
                    _visualGrid.Rows[row].Height = (int)Math.Clamp(excelHeight * 96d / 72d, 18d, 180d);
                }
            }

            foreach (var cell in visual.Cells)
            {
                if (cell.Row < 1 || cell.Column < 1 || cell.Row > _visualGrid.Rows.Count || cell.Column > _visualGrid.Columns.Count)
                {
                    continue;
                }
                var gridCell = _visualGrid.Rows[cell.Row - 1].Cells[cell.Column - 1];
                gridCell.Value = cell.Value;
                gridCell.ToolTipText = cell.Address;
                var color = ParseColor(cell.FillColor);
                if (color.HasValue)
                {
                    gridCell.Style.BackColor = color.Value;
                    gridCell.Style.ForeColor = IsDark(color.Value) ? Color.White : Color.Black;
                }
                var fontStyle = (cell.Bold ? FontStyle.Bold : FontStyle.Regular) |
                                (cell.Italic ? FontStyle.Italic : FontStyle.Regular);
                if (fontStyle != FontStyle.Regular)
                {
                    gridCell.Style.Font = GetCellFont(fontStyle);
                }
            }
        }
        finally
        {
            _visualGrid.ResumeLayout();
        }
    }

    private void PopulateRecordFilters()
    {
        if (_analysis is null)
        {
            return;
        }

        SetFilterItems(_recordTypeFilter, _analysis.Records.Select(x => RecordTypeText(x.RecordType, _analysis.Labels)));
        SetFilterItems(_sectionFilter, _analysis.Records.Select(x => x.Section));
        SetFilterItems(_collaboratorFilter, _analysis.Records.Select(x => x.Owner));
        SetFilterItems(_periodFilter, _analysis.Records.Select(x => x.Period));
        SetFilterItems(_statusFilter, _analysis.Records.Select(x => string.IsNullOrWhiteSpace(x.Value) ? "(vazio)" : x.Value));
    }

    private void ApplyRecordFilters()
    {
        if (_analysis is null)
        {
            _recordsGrid.DataSource = new List<RecordRow>();
            return;
        }

        var type = FilterValue(_recordTypeFilter);
        var section = FilterValue(_sectionFilter);
        var collaborator = FilterValue(_collaboratorFilter);
        var period = FilterValue(_periodFilter);
        var status = FilterValue(_statusFilter);
        var search = _companyFilter.Text.Trim();

        var query = _analysis.Records.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(x => string.Equals(RecordTypeText(x.RecordType, _analysis.Labels), type, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(section))
        {
            query = query.Where(x => string.Equals(x.Section, section, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(collaborator))
        {
            query = query.Where(x => string.Equals(x.Owner, collaborator, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(period))
        {
            query = query.Where(x => string.Equals(x.Period, period, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => string.Equals(string.IsNullOrWhiteSpace(x.Value) ? "(vazio)" : x.Value, status, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Entity.Contains(search, StringComparison.OrdinalIgnoreCase) || x.Code.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        _recordsGrid.DataSource = query
            .OrderBy(x => x.Worksheet, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Entity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Period, StringComparer.OrdinalIgnoreCase)
            .Take(20000)
            .Select(x => new RecordRow
            {
                Type = RecordTypeText(x.RecordType, _analysis.Labels),
                Worksheet = x.Worksheet,
                Section = x.Section,
                Entity = x.Entity,
                Code = x.Code,
                Owner = x.Owner,
                Period = x.Period,
                Value = string.IsNullOrWhiteSpace(x.Value) ? (x.RecordType == "Status" ? "(vazio)" : string.Empty) : x.Value,
                Meaning = string.IsNullOrWhiteSpace(x.ValueMeaning) ? x.StatusMeaning : x.ValueMeaning,
                CurrentValue = x.CurrentValue,
                Count = x.Count,
                CellAddress = x.CellAddress
            }).ToList();
    }

    private void ClearRecordFilters()
    {
        foreach (var combo in new[] { _recordTypeFilter, _sectionFilter, _collaboratorFilter, _periodFilter, _statusFilter })
        {
            if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }
        }
        _companyFilter.Clear();
        ApplyRecordFilters();
    }

    private void OpenWorkbook()
    {
        var path = _analysis?.FilePath ?? GetConfiguredFilePath(_selectedSource?.Source);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(this, "O arquivo configurado não foi encontrado.", "FlowSentinel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async Task RunBusyAsync(string message, Func<Task> operation)
    {
        try
        {
            UseWaitCursor = true;
            Enabled = false;
            _status.Text = message;
            await operation();
        }
        catch (Exception exception)
        {
            VisualEditorSupport.ShowError(this, exception, "Painel de monitoramento");
            _status.Text = "Falha: " + exception.Message;
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    private static bool IsAdministrativeExcelSource(DataSourceDefinition source)
    {
        if (source.Type != SourceType.Excel)
        {
            return false;
        }
        try
        {
            return source.Configuration.TryGetProperty("mode", out var mode) &&
                   string.Equals(mode.GetString(), "SectionedMatrix", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string GetConfiguredFilePath(DataSourceDefinition? source)
    {
        if (source is null)
        {
            return string.Empty;
        }
        return source.Configuration.TryGetProperty("filePath", out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    private void ConfigureVisualGrid()
    {
        _visualGrid.Dock = DockStyle.Fill;
        _visualGrid.ReadOnly = true;
        _visualGrid.AllowUserToAddRows = false;
        _visualGrid.AllowUserToDeleteRows = false;
        _visualGrid.AllowUserToResizeRows = false;
        _visualGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        _visualGrid.RowHeadersVisible = true;
        _visualGrid.RowHeadersWidth = 58;
        _visualGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _visualGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        _visualGrid.BackgroundColor = Color.White;
    }

    private static DataGridView CreateReadOnlyGrid() => new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };

    private static DataGridViewTextBoxColumn TextColumn(string property, string title, float weight) => new()
    {
        DataPropertyName = property,
        HeaderText = title,
        FillWeight = weight,
        SortMode = DataGridViewColumnSortMode.Automatic
    };

    private static void AddButton(Control parent, string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(3) };
        button.Click += handler;
        parent.Controls.Add(button);
    }

    private static Control Labeled(string label, Control input)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(4) };
        panel.Controls.Add(new Label { Text = label, AutoSize = true });
        panel.Controls.Add(input);
        return panel;
    }

    private static void ConfigureFilter(ComboBox combo, int width)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Width = width;
    }

    private static void SetFilterItems(ComboBox combo, IEnumerable<string> values)
    {
        combo.Items.Clear();
        combo.Items.Add("Todos");
        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            combo.Items.Add(value);
        }
        combo.SelectedIndex = 0;
    }

    private static string FilterValue(ComboBox combo) =>
        combo.SelectedIndex <= 0 ? string.Empty : combo.SelectedItem?.ToString() ?? string.Empty;

    private void ApplyConfiguredLabels(WorkbookMonitoringLabels labels)
    {
        SetColumnHeader(_recordsGrid, nameof(RecordRow.Section), labels.Category);
        SetColumnHeader(_recordsGrid, nameof(RecordRow.Entity), labels.EntitySingular);
        SetColumnHeader(_recordsGrid, nameof(RecordRow.Code), labels.Code);
        SetColumnHeader(_recordsGrid, nameof(RecordRow.Owner), labels.Owner);
        SetColumnHeader(_recordsGrid, nameof(RecordRow.Period), labels.Period);
        SetColumnHeader(_recordsGrid, nameof(RecordRow.Value), labels.Value);
        SetColumnHeader(_recordsGrid, nameof(RecordRow.CurrentValue), $"{labels.Value} atual");

        SetColumnHeader(_changesGrid, nameof(ChangeRow.Section), labels.Category);
        SetColumnHeader(_changesGrid, nameof(ChangeRow.Entity), labels.EntitySingular);
        SetColumnHeader(_changesGrid, nameof(ChangeRow.Code), labels.Code);
        SetColumnHeader(_changesGrid, nameof(ChangeRow.Period), labels.Period);

        _companyFilter.PlaceholderText = $"{labels.EntitySingular} ou {labels.Code.ToLowerInvariant()}";
    }

    private static void SetColumnHeader(DataGridView grid, string propertyName, string title)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (string.Equals(column.DataPropertyName, propertyName, StringComparison.Ordinal))
            {
                column.HeaderText = title;
                break;
            }
        }
    }

    private static string RecordTypeText(string value, WorkbookMonitoringLabels labels) => value switch
    {
        "Company" or "Entity" => labels.EntitySingular,
        "Status" => labels.Value,
        "Aggregate" => "Quantidade",
        _ => value
    };

    private static string MetricText(string value, WorkbookMonitoringLabels labels) => value switch
    {
        "CompaniesByCurrentStatus" or "EntitiesByCurrentValue" => $"{labels.EntityPlural} por {labels.Value.ToLowerInvariant()} atual",
        "StatusCells" or "ValuesByPeriod" => $"Valores por {labels.Value.ToLowerInvariant()} e {labels.Period.ToLowerInvariant()}",
        _ => value
    };

    private static string ScopeText(string value, WorkbookMonitoringLabels labels) => value switch
    {
        "Global" => "Geral",
        "Section" or "Category" => labels.Category,
        "Collaborator" or "Owner" => labels.Owner,
        _ => value
    };

    private static ChangeRow MapChange(WorkbookMonitoringChange x, WorkbookMonitoringLabels labels) => new()
    {
        ChangeType = x.ChangeType,
        RecordType = RecordTypeText(x.RecordType, labels),
        Worksheet = x.Worksheet,
        Section = string.IsNullOrWhiteSpace(x.Category) ? x.Section : x.Category,
        Entity = string.IsNullOrWhiteSpace(x.Entity) ? x.Company : x.Entity,
        Code = x.Code,
        Period = x.Period,
        ChangedFields = x.ChangedFields,
        PreviousValue = x.PreviousValue,
        CurrentValue = x.CurrentValue,
        CellAddress = x.CellAddress
    };

    private static Color? ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        try
        {
            return ColorTranslator.FromHtml(value);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsDark(Color color) => (color.R * 299 + color.G * 587 + color.B * 114) / 1000 < 128;

    private static string ExcelColumnName(int number)
    {
        var name = string.Empty;
        while (number > 0)
        {
            number--;
            name = (char)('A' + number % 26) + name;
            number /= 26;
        }
        return name;
    }


    private Font GetCellFont(FontStyle style)
    {
        if (_cellFonts.TryGetValue(style, out var font))
        {
            return font;
        }
        font = new Font(_visualGrid.Font, style);
        _cellFonts[style] = font;
        return font;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var font in _cellFonts.Values)
            {
                font.Dispose();
            }
            _cellFonts.Clear();
        }
        base.Dispose(disposing);
    }

    private sealed record MonitoringSourceItem(Guid AutomationId, string AutomationName, DataSourceDefinition Source)
    {
        public Guid SourceId => Source.Id;
        public override string ToString() => $"{AutomationName} — {Source.Name}";
    }

    private sealed class SummaryRow
    {
        public string Metric { get; init; } = string.Empty;
        public string Scope { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public string Worksheet { get; init; } = string.Empty;
        public string Period { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string Meaning { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed class RecordRow
    {
        public string Type { get; init; } = string.Empty;
        public string Worksheet { get; init; } = string.Empty;
        public string Section { get; init; } = string.Empty;
        public string Entity { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Owner { get; init; } = string.Empty;
        public string Period { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Meaning { get; init; } = string.Empty;
        public string CurrentValue { get; init; } = string.Empty;
        public int? Count { get; init; }
        public string CellAddress { get; init; } = string.Empty;
    }

    private sealed class ChangeRow
    {
        public string ChangeType { get; init; } = string.Empty;
        public string RecordType { get; init; } = string.Empty;
        public string Worksheet { get; init; } = string.Empty;
        public string Section { get; init; } = string.Empty;
        public string Entity { get; init; } = string.Empty;
        public string Code { get; init; } = string.Empty;
        public string Period { get; init; } = string.Empty;
        public string ChangedFields { get; init; } = string.Empty;
        public string PreviousValue { get; init; } = string.Empty;
        public string CurrentValue { get; init; } = string.Empty;
        public string CellAddress { get; init; } = string.Empty;
    }
}
