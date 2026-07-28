using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowSentinel.Domain;

public sealed class AutomationDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
    public int Priority { get; set; } = 100;
    public MissingRecordBehavior MissingRecordBehavior { get; set; } = MissingRecordBehavior.Ignore;
    public bool ResolveWhenPersistenceFails { get; set; } = true;
    public List<DataSourceDefinition> Sources { get; set; } = [];
    public RuleSetDefinition EntryRules { get; set; } = RuleSetDefinition.AlwaysTrue(RuleSetType.Entry);
    public RuleSetDefinition? PersistenceRules { get; set; }
    public RuleSetDefinition? CompletionRules { get; set; }
    public RuleSetDefinition? SuspensionRules { get; set; }
    public List<ActionDefinition> Actions { get; set; } = [];
    public List<ContactGroupDefinition> ContactGroups { get; set; } = [];

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("A automação precisa possuir um identificador.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Informe o nome da automação.");
        }

        if (IntervalSeconds < 5)
        {
            throw new InvalidOperationException("O intervalo mínimo é de 5 segundos.");
        }

        if (Sources.Count == 0)
        {
            throw new InvalidOperationException("A automação precisa possuir ao menos uma fonte.");
        }

        if (Sources.Count(x => x.IsPrimary) != 1)
        {
            throw new InvalidOperationException("Defina exatamente uma fonte primária.");
        }

        if (!Sources.Single(x => x.IsPrimary).Enabled)
        {
            throw new InvalidOperationException("A fonte primária precisa estar habilitada.");
        }

        var sourceIds = new HashSet<Guid>();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in Sources)
        {
            source.Validate();
            if (!sourceIds.Add(source.Id))
            {
                throw new InvalidOperationException($"O identificador da fonte '{source.Name}' está duplicado.");
            }
            if (!aliases.Add(source.Alias))
            {
                throw new InvalidOperationException($"O alias de fonte '{source.Alias}' está duplicado.");
            }
        }

        EntryRules.Validate();
        PersistenceRules?.Validate();
        CompletionRules?.Validate();
        SuspensionRules?.Validate();

        var actionIds = new HashSet<Guid>();
        foreach (var action in Actions)
        {
            action.Validate();
            if (!actionIds.Add(action.Id))
            {
                throw new InvalidOperationException($"O identificador da ação '{action.Name}' está duplicado.");
            }
        }

        var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in ContactGroups)
        {
            if (string.IsNullOrWhiteSpace(group.Id) || string.IsNullOrWhiteSpace(group.Name))
            {
                throw new InvalidOperationException("Todo grupo de contatos precisa possuir ID e nome.");
            }
            if (!groupIds.Add(group.Id))
            {
                throw new InvalidOperationException($"O grupo de contatos '{group.Id}' está duplicado.");
            }
        }
    }
}

public sealed class DataSourceDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Alias { get; set; } = "primary";
    public string Name { get; set; } = string.Empty;
    public SourceType Type { get; set; }
    public bool IsPrimary { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string> KeyFields { get; set; } = [];
    public JsonElement Configuration { get; set; } = CreateEmptyConfiguration();

    private static JsonElement CreateEmptyConfiguration()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("Toda fonte precisa possuir um identificador.");
        }

        if (string.IsNullOrWhiteSpace(Alias))
        {
            throw new InvalidOperationException("Toda fonte precisa possuir um alias.");
        }

        if (KeyFields.Count == 0 || KeyFields.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"A fonte '{Alias}' precisa possuir ao menos um campo-chave válido.");
        }
    }
}

public sealed class RuleSetDefinition
{
    public RuleSetType Type { get; set; }
    public RuleGroupDefinition Root { get; set; } = new();

    public static RuleSetDefinition AlwaysTrue(RuleSetType type) => new()
    {
        Type = type,
        Root = new RuleGroupDefinition
        {
            Operator = LogicalOperator.And,
            Rules = []
        }
    };

    public void Validate() => Root.Validate();
}

public sealed class RuleGroupDefinition
{
    public LogicalOperator Operator { get; set; } = LogicalOperator.And;
    public bool Negate { get; set; }
    public List<RuleDefinition> Rules { get; set; } = [];
    public List<RuleGroupDefinition> Groups { get; set; } = [];

    public void Validate()
    {
        foreach (var rule in Rules)
        {
            rule.Validate();
        }

        foreach (var group in Groups)
        {
            group.Validate();
        }
    }
}

public sealed class RuleDefinition
{
    public string Field { get; set; } = string.Empty;
    public RuleOperator Operator { get; set; }
    public string? ExpectedValue { get; set; }
    public string? ExpectedField { get; set; }
    public bool CaseSensitive { get; set; }
    public string? Culture { get; set; } = "pt-BR";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Field))
        {
            throw new InvalidOperationException("Toda regra precisa informar o campo avaliado.");
        }
    }
}

public sealed class ActionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public ActionTrigger Trigger { get; set; } = ActionTrigger.OnOpen;
    public int DelaySeconds { get; set; }
    public RepeatPolicyDefinition Repeat { get; set; } = new();
    public ChannelExecutionStrategy ChannelStrategy { get; set; } = ChannelExecutionStrategy.All;
    public ActionSuccessPolicy SuccessPolicy { get; set; } = ActionSuccessPolicy.AllDeliveries;
    public RuleSetDefinition? Conditions { get; set; }
    public string SubjectTemplate { get; set; } = "{{automation.name}}";
    public string MessageTemplate { get; set; } = string.Empty;
    public List<ActionChannelDefinition> Channels { get; set; } = [];
    public List<RecipientDefinition> Recipients { get; set; } = [];

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("A ação precisa possuir um identificador.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Informe o nome da ação.");
        }

        if (Channels.Count == 0)
        {
            throw new InvalidOperationException($"A ação '{Name}' precisa possuir ao menos um canal.");
        }

        if (Channels.Any(x => x.ChannelConfigurationId == Guid.Empty))
        {
            throw new InvalidOperationException($"A ação '{Name}' possui um canal sem configuração vinculada.");
        }

        if (Recipients.Count == 0 && Channels.Any(x => x.ChannelType != ChannelType.LocalWindows))
        {
            throw new InvalidOperationException($"A ação '{Name}' precisa possuir destinatários.");
        }

        if (Recipients.Any(x => string.IsNullOrWhiteSpace(x.Value)))
        {
            throw new InvalidOperationException($"A ação '{Name}' possui um destinatário sem valor, campo ou grupo.");
        }

        if (DelaySeconds < 0)
        {
            throw new InvalidOperationException($"O atraso da ação '{Name}' não pode ser negativo.");
        }

        if (Repeat.Enabled && Repeat.IntervalSeconds < 1)
        {
            throw new InvalidOperationException($"O intervalo de repetição da ação '{Name}' deve ser maior que zero.");
        }

        Conditions?.Validate();
    }
}

public sealed class RepeatPolicyDefinition
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 3600;
    public int MaxExecutions { get; set; } = 1;

    public bool AllowsExecution(int previousExecutions)
    {
        if (previousExecutions == 0)
        {
            return true;
        }

        return Enabled && (MaxExecutions <= 0 || previousExecutions < MaxExecutions);
    }
}

public sealed class ActionChannelDefinition
{
    public Guid ChannelConfigurationId { get; set; }
    public ChannelType ChannelType { get; set; }
    public int Order { get; set; }
    public bool Required { get; set; }
}

public sealed class RecipientDefinition
{
    public RecipientType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public ChannelType? ChannelType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ContactGroupDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<ContactDefinition> Contacts { get; set; } = [];
}

public sealed class ContactDefinition
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<ChannelType, List<string>> Addresses { get; set; } = [];
}

public static class FlowJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
