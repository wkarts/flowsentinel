using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class WorkbookMonitoringAnalysis
{
    public required Guid SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string FilePath { get; init; }
    public required IReadOnlyList<string> Worksheets { get; init; }
    public required IReadOnlyList<WorkbookMonitoringRecord> Records { get; init; }
    public required IReadOnlyList<WorkbookStatusSummary> StatusSummaries { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required IReadOnlyList<WorkbookWorksheetVisual> Visuals { get; init; }
    public WorkbookMonitoringLabels Labels { get; init; } = new();
    public DateTimeOffset AnalyzedAt { get; init; } = DateTimeOffset.Now;

    public WorkbookWorksheetVisual? Visual => Visuals.FirstOrDefault();

    public int EntityCount => Records
        .Where(x => string.Equals(x.RecordType, "Company", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.RecordType, "Entity", StringComparison.OrdinalIgnoreCase))
        .Select(x => string.IsNullOrWhiteSpace(x.EntityKey) ? x.CompanyKey : x.EntityKey)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    // Compatibilidade com automações criadas durante a prévia 0.3.0.
    public int CompanyCount => EntityCount;

    public int StatusCellCount => Records.Count(x => string.Equals(x.RecordType, "Status", StringComparison.OrdinalIgnoreCase));
    public int HighlightedCellCount => Records.Count(x => x.IsHighlighted);
    public int BlankStatusCount => Records.Count(x => string.Equals(x.RecordType, "Status", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(x.Value));
    public int SectionCount => Records.Select(x => string.IsNullOrWhiteSpace(x.Category) ? x.Section : x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
}


public sealed class WorkbookMonitoringLabels
{
    public string ProfileName { get; init; } = string.Empty;
    public string EntitySingular { get; init; } = "Registro";
    public string EntityPlural { get; init; } = "Registros";
    public string Owner { get; init; } = "Responsável";
    public string Category { get; init; } = "Grupo";
    public string Period { get; init; } = "Período";
    public string Code { get; init; } = "Código";
    public string Value { get; init; } = "Valor";
}

public sealed class WorkbookMonitoringRecord
{
    public required string Key { get; init; }
    public required string Fingerprint { get; init; }
    public string RecordType { get; init; } = string.Empty;
    public string Worksheet { get; init; } = string.Empty;
    public string Year { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public string Regime { get; init; } = string.Empty;
    public string EntityKey { get; init; } = string.Empty;
    public string Entity { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;
    public string ValueMeaning { get; init; } = string.Empty;

    // Aliases legados preservados para regras e templates já existentes.
    public string CompanyKey { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Collaborator { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string CurrentPeriod { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public string StatusMeaning { get; init; } = string.Empty;
    public string Metric { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public int? Count { get; init; }
    public string CellAddress { get; init; } = string.Empty;
    public string FillColor { get; init; } = string.Empty;
    public bool IsHighlighted { get; init; }
    public int RowNumber { get; init; }
    public int ColumnNumber { get; init; }
    public IReadOnlyDictionary<string, string?> Fields { get; init; } =
        new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class WorkbookStatusSummary
{
    public string Metric { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
    public string Worksheet { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusMeaning { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class WorkbookMonitoringComparison
{
    public bool BaselineExists { get; init; }
    public DateTimeOffset? BaselineCreatedAt { get; init; }
    public required IReadOnlyList<WorkbookMonitoringChange> Changes { get; init; }
}

public sealed class WorkbookMonitoringChange
{
    public string ChangeType { get; init; } = string.Empty;
    public string RecordType { get; init; } = string.Empty;
    public string Worksheet { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public string Entity { get; init; } = string.Empty;
    public string Owner { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Collaborator { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string CellAddress { get; init; } = string.Empty;
    public string ChangedFields { get; init; } = string.Empty;
    public string PreviousValue { get; init; } = string.Empty;
    public string CurrentValue { get; init; } = string.Empty;
}

public sealed class WorkbookWorksheetVisual
{
    public string Worksheet { get; init; } = string.Empty;
    public int RowCount { get; init; }
    public int ColumnCount { get; init; }
    public required IReadOnlyList<WorkbookVisualCell> Cells { get; init; }
    public IReadOnlyDictionary<int, double> ColumnWidths { get; init; } = new Dictionary<int, double>();
    public IReadOnlyDictionary<int, double> RowHeights { get; init; } = new Dictionary<int, double>();
}

public sealed class WorkbookVisualCell
{
    public int Row { get; init; }
    public int Column { get; init; }
    public string Address { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string FillColor { get; init; } = string.Empty;
    public bool IsHighlighted { get; init; }
    public bool Bold { get; init; }
    public bool Italic { get; init; }
}

public interface IWorkbookMonitoringService
{
    Task<WorkbookMonitoringAnalysis> AnalyzeAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken);

    Task<WorkbookMonitoringComparison> CompareWithBaselineAsync(
        WorkbookMonitoringAnalysis current,
        CancellationToken cancellationToken);

    Task SaveBaselineAsync(
        WorkbookMonitoringAnalysis analysis,
        CancellationToken cancellationToken);
}
