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
    public string ProfileName { get; set; } = string.Empty;
    public ExcelMatrixSettings Matrix { get; set; } = new();
}

internal sealed class ExcelMatrixSettings
{
    public string HeaderMarker { get; set; } = string.Empty;
    public string HeaderTextContains { get; set; } = string.Empty;
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

    // Todos os valores abaixo são configuráveis. O parser não conhece termos contábeis.
    public string PeriodLabels { get; set; } = string.Empty;
    public string StandaloneSectionTitles { get; set; } = string.Empty;
    public string SectionTitlePrefixes { get; set; } = string.Empty;
    public string SectionNamePrefixesToRemove { get; set; } = string.Empty;
    public string SectionsWithoutPeriods { get; set; } = string.Empty;
    public string CurrentStatusExcludedPeriods { get; set; } = string.Empty;
    public string CurrentStatusMode { get; set; } = "LastFilled";
    public Dictionary<string, int> CalendarPeriodNumbers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> StatusLabels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Rótulos administrativos. Mantêm o mesmo motor útil para clientes, equipamentos,
    // tarefas, documentos, contratos ou qualquer outra entidade representada nas linhas.
    public string EntitySingularName { get; set; } = "Registro";
    public string EntityPluralName { get; set; } = "Registros";
    public string OwnerName { get; set; } = "Responsável";
    public string CategoryName { get; set; } = "Grupo";
    public string PeriodName { get; set; } = "Período";
    public string CodeName { get; set; } = "Código";
    public string ValueName { get; set; } = "Valor";
}
