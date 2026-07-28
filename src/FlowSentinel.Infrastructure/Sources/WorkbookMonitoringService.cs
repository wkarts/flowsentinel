using System.Text.Json;
using ClosedXML.Excel;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal sealed class WorkbookMonitoringService : IWorkbookMonitoringService
{
    private readonly IDataSourceReader _excelReader;
    private readonly AppPaths _paths;

    public WorkbookMonitoringService(
        IEnumerable<IDataSourceReader> readers,
        AppPaths paths)
    {
        _excelReader = readers.Single(x => x.SourceType == SourceType.Excel);
        _paths = paths;
    }

    public async Task<WorkbookMonitoringAnalysis> AnalyzeAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken)
    {
        if (source.Type != SourceType.Excel)
        {
            throw new InvalidOperationException("O painel de planilhas aceita somente fontes Excel.");
        }

        var settings = source.Configuration.Deserialize<ExcelSourceSettings>(FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração de Excel inválida.");
        if (!settings.Mode.Equals("SectionedMatrix", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Esta fonte ainda está no modo Tabela simples. Edite a fonte e selecione 'Matriz com múltiplas seções e períodos'.");
        }

        var result = await _excelReader.ReadAsync(source, cancellationToken);
        var monitoringRecords = result.Records.Select(MapRecord).ToArray();
        var summaries = monitoringRecords
            .Where(x => string.Equals(x.RecordType, "Aggregate", StringComparison.OrdinalIgnoreCase))
            .Select(x => new WorkbookStatusSummary
            {
                Metric = x.Metric,
                Unit = x.Unit,
                Scope = x.Scope,
                Group = x.Group,
                Worksheet = x.Worksheet,
                Period = x.Period,
                Status = string.IsNullOrWhiteSpace(x.Status) ? "(vazio)" : x.Status,
                StatusMeaning = x.StatusMeaning,
                Count = x.Count ?? 0
            })
            .OrderBy(x => x.Worksheet, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Scope, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Group, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Period, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Status, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var path = SourceReaderHelpers.ResolvePath(settings.FilePath);
        var visuals = await BuildVisualsAsync(path, settings, cancellationToken);
        return new WorkbookMonitoringAnalysis
        {
            SourceId = source.Id,
            SourceName = source.Name,
            FilePath = path,
            Worksheets = monitoringRecords.Select(x => x.Worksheet)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Records = monitoringRecords,
            StatusSummaries = summaries,
            Warnings = result.Warnings.ToArray(),
            Visuals = visuals,
            AnalyzedAt = DateTimeOffset.Now
        };
    }

    public async Task<WorkbookMonitoringComparison> CompareWithBaselineAsync(
        WorkbookMonitoringAnalysis current,
        CancellationToken cancellationToken)
    {
        var path = BaselinePath(current.SourceId);
        if (!File.Exists(path))
        {
            return new WorkbookMonitoringComparison
            {
                BaselineExists = false,
                Changes = []
            };
        }

        await using var stream = File.OpenRead(path);
        var baseline = await JsonSerializer.DeserializeAsync<BaselineDocument>(stream, FlowJson.Options, cancellationToken)
                       ?? throw new InvalidOperationException("A linha de base da planilha está inválida.");
        var previous = baseline.Records.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var actual = current.Records.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
        var changes = new List<WorkbookMonitoringChange>();

        foreach (var record in actual.Values.Where(x => !previous.ContainsKey(x.Key)))
        {
            changes.Add(CreateChange("Adicionado", null, record));
        }

        foreach (var record in previous.Values.Where(x => !actual.ContainsKey(x.Key)))
        {
            changes.Add(CreateChange("Removido", record, null));
        }

        foreach (var pair in actual)
        {
            if (!previous.TryGetValue(pair.Key, out var oldRecord) ||
                string.Equals(oldRecord.Fingerprint, pair.Value.Fingerprint, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            changes.Add(CreateChange("Alterado", oldRecord, pair.Value));
        }

        return new WorkbookMonitoringComparison
        {
            BaselineExists = true,
            BaselineCreatedAt = baseline.CreatedAt,
            Changes = changes
                .OrderBy(x => x.Worksheet, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Section, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Company, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Period, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public async Task SaveBaselineAsync(
        WorkbookMonitoringAnalysis analysis,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_paths.DataDirectory, "monitor-baselines");
        Directory.CreateDirectory(directory);
        var document = new BaselineDocument
        {
            SourceId = analysis.SourceId,
            CreatedAt = DateTimeOffset.Now,
            Records = analysis.Records.Select(x => new BaselineRecord
            {
                Key = x.Key,
                Fingerprint = x.Fingerprint,
                RecordType = x.RecordType,
                Worksheet = x.Worksheet,
                Section = x.Section,
                Company = x.Company,
                Code = x.Code,
                Collaborator = x.Collaborator,
                Period = x.Period,
                Status = x.Status,
                Count = x.Count,
                CellAddress = x.CellAddress,
                FillColor = x.FillColor,
                Fields = x.Fields.ToDictionary(y => y.Key, y => y.Value, StringComparer.OrdinalIgnoreCase)
            }).ToList()
        };

        await using var stream = File.Create(BaselinePath(analysis.SourceId));
        await JsonSerializer.SerializeAsync(stream, document, FlowJson.Options, cancellationToken);
    }

    private async Task<IReadOnlyList<WorkbookWorksheetVisual>> BuildVisualsAsync(
        string path,
        ExcelSourceSettings settings,
        CancellationToken cancellationToken)
    {
        var snapshot = await SourceReaderHelpers.CreateSnapshotAsync(path, cancellationToken);
        try
        {
            using var workbook = new XLWorkbook(snapshot);
            var visuals = new List<WorkbookWorksheetVisual>();
            foreach (var worksheet in ExcelSectionedMatrixParser.ResolveWorksheets(workbook, settings))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var data = ExcelSectionedMatrixParser.BuildVisual(worksheet);
                visuals.Add(new WorkbookWorksheetVisual
                {
                    Worksheet = data.Worksheet,
                    RowCount = data.RowCount,
                    ColumnCount = data.ColumnCount,
                    Cells = data.Cells.Select(x => new WorkbookVisualCell
                    {
                        Row = x.Row,
                        Column = x.Column,
                        Address = x.Address,
                        Value = x.Value,
                        FillColor = x.FillColor,
                        IsHighlighted = x.IsHighlighted,
                        Bold = x.Bold,
                        Italic = x.Italic
                    }).ToArray(),
                    ColumnWidths = data.ColumnWidths,
                    RowHeights = data.RowHeights
                });
            }
            return visuals;
        }
        finally
        {
            try { File.Delete(snapshot); } catch { }
        }
    }

    private string BaselinePath(Guid sourceId) =>
        Path.Combine(_paths.DataDirectory, "monitor-baselines", $"{sourceId:N}.json");

    private static WorkbookMonitoringRecord MapRecord(DataRecord record)
    {
        string Get(string key) => record.Fields.GetValueOrDefault(key) ?? string.Empty;
        int? GetInt(string key) => int.TryParse(Get(key), out var value) ? value : null;
        return new WorkbookMonitoringRecord
        {
            Key = record.Key,
            Fingerprint = record.Fingerprint,
            RecordType = Get("__recordType"),
            Worksheet = Get("Worksheet"),
            Year = Get("Year"),
            Section = Get("Section"),
            Regime = Get("Regime"),
            CompanyKey = Get("CompanyKey"),
            Company = Get("Company"),
            Code = Get("Code"),
            Collaborator = Get("Collaborator"),
            Period = Get("Period"),
            CurrentPeriod = Get("CurrentPeriod"),
            Status = Get("Status"),
            CurrentStatus = Get("CurrentStatus"),
            StatusMeaning = Get("StatusMeaning"),
            Metric = Get("Metric"),
            Unit = Get("Unit"),
            Scope = Get("Scope"),
            Group = Get("Group"),
            Count = GetInt("Count"),
            CellAddress = Get("CellAddress"),
            FillColor = Get("FillColor"),
            IsHighlighted = bool.TryParse(Get("IsHighlighted"), out var highlighted) && highlighted,
            RowNumber = GetInt("RowNumber") ?? 0,
            ColumnNumber = GetInt("ColumnNumber") ?? 0,
            Fields = record.Fields
        };
    }

    private static WorkbookMonitoringChange CreateChange(
        BaselineRecord? previous,
        WorkbookMonitoringRecord? current) => CreateChange("Alterado", previous, current);

    private static WorkbookMonitoringChange CreateChange(
        string type,
        BaselineRecord? previous,
        WorkbookMonitoringRecord? current)
    {
        var oldFields = previous?.Fields ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var newFields = current?.Fields ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var keys = oldFields.Keys.Concat(newFields.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => !x.StartsWith("__", StringComparison.Ordinal))
            .Where(x => !string.Equals(oldFields.GetValueOrDefault(x), newFields.GetValueOrDefault(x), StringComparison.Ordinal))
            .ToArray();

        var oldValue = DisplayValue(previous?.RecordType, previous?.Count, oldFields, previous?.Status, previous?.Collaborator);
        var newValue = DisplayValue(current?.RecordType, current?.Count, newFields, current?.Status, current?.Collaborator);

        return new WorkbookMonitoringChange
        {
            ChangeType = type,
            RecordType = current?.RecordType ?? previous?.RecordType ?? string.Empty,
            Worksheet = current?.Worksheet ?? previous?.Worksheet ?? string.Empty,
            Section = current?.Section ?? previous?.Section ?? string.Empty,
            Company = current?.Company ?? previous?.Company ?? string.Empty,
            Code = current?.Code ?? previous?.Code ?? string.Empty,
            Collaborator = current?.Collaborator ?? previous?.Collaborator ?? string.Empty,
            Period = current?.Period ?? previous?.Period ?? string.Empty,
            CellAddress = current?.CellAddress ?? previous?.CellAddress ?? string.Empty,
            ChangedFields = string.Join(", ", keys),
            PreviousValue = oldValue,
            CurrentValue = newValue
        };
    }


    private static string DisplayValue(
        string? recordType,
        int? count,
        IReadOnlyDictionary<string, string?> fields,
        string? status,
        string? collaborator)
    {
        if (string.Equals(recordType, "Aggregate", StringComparison.OrdinalIgnoreCase))
        {
            return count?.ToString() ?? fields.GetValueOrDefault("Count") ?? string.Empty;
        }
        if (string.Equals(recordType, "Company", StringComparison.OrdinalIgnoreCase))
        {
            return fields.GetValueOrDefault("CurrentStatus") ?? collaborator ?? string.Empty;
        }
        return status ?? fields.GetValueOrDefault("Status") ?? string.Empty;
    }
    private sealed class BaselineDocument
    {
        public Guid SourceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<BaselineRecord> Records { get; set; } = [];
    }

    private sealed class BaselineRecord
    {
        public string Key { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = string.Empty;
        public string RecordType { get; set; } = string.Empty;
        public string Worksheet { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string Company { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Collaborator { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? Count { get; set; }
        public string CellAddress { get; set; } = string.Empty;
        public string FillColor { get; set; } = string.Empty;
        public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
