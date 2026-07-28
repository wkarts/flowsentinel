namespace FlowSentinel.Infrastructure.Sources;

internal sealed class ExcelSourceSettings
{
    public string FilePath { get; set; } = string.Empty;
    public string? Worksheet { get; set; }
    public int HeaderRow { get; set; } = 1;
    public bool IgnoreEmptyRows { get; set; } = true;
    public string Mode { get; set; } = "Table";
    public string WorksheetSelection { get; set; } = "Fixed";
    public string WorksheetPattern { get; set; } = @"(?<year>20\d{2})";
    public ExcelMatrixSettings Matrix { get; set; } = new();
}

internal sealed class ExcelMatrixSettings
{
    public string HeaderMarker { get; set; } = "Nº";
    public int NumberColumn { get; set; } = 1;
    public int SectionColumn { get; set; } = 2;
    public int CompanyColumn { get; set; } = 2;
    public int CodeColumn { get; set; } = 3;
    public int CollaboratorColumn { get; set; } = 4;
    public int FirstPeriodColumn { get; set; } = 5;
    public int LastPeriodColumn { get; set; } = 20;
    public bool IncludeBlankStatuses { get; set; } = true;
    public bool IncludeFormatting { get; set; } = true;
    public bool GenerateCompanyRecords { get; set; } = true;
    public bool GenerateAggregateRecords { get; set; } = true;
    public bool AggregateBySection { get; set; } = true;
    public bool AggregateByCollaborator { get; set; } = true;
    public bool AutoDetectStandaloneSections { get; set; } = true;
    public string StandaloneSectionTitles { get; set; } = "SIMPLES|EMPRESAS MEI|SEM MOVIMENTO";
    public string SectionsWithoutPeriods { get; set; } = "EMPRESAS MEI|SEM MOVIMENTO";
    public string CurrentStatusExcludedPeriods { get; set; } = "BAL";
    public string CurrentStatusMode { get; set; } = "CalendarPeriod";
    public Dictionary<string, string> StatusLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
