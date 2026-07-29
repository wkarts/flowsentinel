namespace FlowSentinel.Desktop;

internal enum WorkbookTemplateKind
{
    Rp102,
    PeriodicMatrix,
    TaskTracking,
    DocumentControl,
    Custom
}

internal sealed class WorkbookTemplateProfile
{
    internal WorkbookTemplateKind Kind { get; init; }
    internal string DisplayName { get; init; } = string.Empty;
    internal string Description { get; init; } = string.Empty;
    internal string DefaultMonitoringName { get; init; } = string.Empty;
    internal string HeaderMarker { get; init; } = string.Empty;
    internal string HeaderTextContains { get; init; } = string.Empty;
    internal string PeriodLabels { get; init; } = string.Empty;
    internal string SectionTitlePrefixes { get; init; } = string.Empty;
    internal string SectionNamePrefixesToRemove { get; init; } = string.Empty;
    internal string StandaloneSectionTitles { get; init; } = string.Empty;
    internal string SectionsWithoutPeriods { get; init; } = string.Empty;
    internal string CurrentValueExcludedPeriods { get; init; } = string.Empty;
    internal string CurrentValueMode { get; init; } = "LastFilled";
    internal int NumberColumn { get; init; } = 1;
    internal int CategoryColumn { get; init; } = 2;
    internal int EntityColumn { get; init; } = 2;
    internal int CodeColumn { get; init; } = 3;
    internal int OwnerColumn { get; init; } = 4;
    internal int FirstValueColumn { get; init; } = 5;
    internal int LastValueColumn { get; init; } = 20;
    internal string EntitySingular { get; init; } = "Registro";
    internal string EntityPlural { get; init; } = "Registros";
    internal string OwnerName { get; init; } = "Responsável";
    internal string CategoryName { get; init; } = "Grupo";
    internal string PeriodName { get; init; } = "Período";
    internal string CodeName { get; init; } = "Código";
    internal string ValueName { get; init; } = "Situação";

    internal static WorkbookTemplateProfile Get(WorkbookTemplateKind kind) => kind switch
    {
        WorkbookTemplateKind.Rp102 => new WorkbookTemplateProfile
        {
            Kind = kind,
            DisplayName = "Matriz contábil RP-102",
            Description = "Configuração inicial para conferência contábil por empresa, regime, responsável e períodos.",
            DefaultMonitoringName = "Monitoramento contábil RP-102",
            HeaderMarker = "Nº",
            HeaderTextContains = "EMPRESAS",
            PeriodLabels = "JAN|FEV|MAR|ABR|MAI|JUN|JUL|AGO|SET|OUT|NOV|DEZ|BAL",
            SectionTitlePrefixes = "EMPRESAS ",
            SectionNamePrefixesToRemove = "EMPRESAS ",
            StandaloneSectionTitles = "SIMPLES|EMPRESAS MEI|SEM MOVIMENTO",
            SectionsWithoutPeriods = "EMPRESAS MEI|SEM MOVIMENTO",
            CurrentValueExcludedPeriods = "BAL",
            CurrentValueMode = "CalendarPeriod",
            EntitySingular = "Empresa",
            EntityPlural = "Clientes",
            OwnerName = "Colaborador",
            CategoryName = "Regime",
            ValueName = "Situação"
        },
        WorkbookTemplateKind.PeriodicMatrix => new WorkbookTemplateProfile
        {
            Kind = kind,
            DisplayName = "Matriz de acompanhamento por períodos",
            Description = "Modelo genérico para entidades organizadas em linhas e períodos ou etapas distribuídos em colunas.",
            DefaultMonitoringName = "Monitoramento de matriz por períodos",
            HeaderMarker = "ID",
            EntitySingular = "Registro",
            EntityPlural = "Registros",
            OwnerName = "Responsável",
            CategoryName = "Grupo",
            ValueName = "Status"
        },
        WorkbookTemplateKind.TaskTracking => new WorkbookTemplateProfile
        {
            Kind = kind,
            DisplayName = "Controle de tarefas e responsáveis",
            Description = "Modelo para tarefas, projetos, responsáveis e evolução por etapas ou períodos.",
            DefaultMonitoringName = "Monitoramento de tarefas",
            HeaderMarker = "ID",
            EntitySingular = "Tarefa",
            EntityPlural = "Tarefas",
            OwnerName = "Responsável",
            CategoryName = "Projeto",
            PeriodName = "Etapa",
            ValueName = "Status"
        },
        WorkbookTemplateKind.DocumentControl => new WorkbookTemplateProfile
        {
            Kind = kind,
            DisplayName = "Controle documental",
            Description = "Modelo para documentos, responsáveis, categorias e estados de conferência ou validade.",
            DefaultMonitoringName = "Monitoramento de documentos",
            HeaderMarker = "ID",
            EntitySingular = "Documento",
            EntityPlural = "Documentos",
            OwnerName = "Responsável",
            CategoryName = "Categoria",
            PeriodName = "Etapa",
            ValueName = "Situação"
        },
        _ => new WorkbookTemplateProfile
        {
            Kind = WorkbookTemplateKind.Custom,
            DisplayName = "Modelo personalizado",
            Description = "Assistente livre para mapear a estrutura, selecionar áreas e definir o comportamento do monitoramento.",
            DefaultMonitoringName = "Novo monitoramento de planilha",
            HeaderMarker = "ID"
        }
    };
}
