using System.Text.Json;
using FlowSentinel.Domain;

namespace FlowSentinel.Domain.Tests;

public sealed class AutomationDefinitionTests
{
    [Fact]
    public void DeveAceitarMultiplasAcoesCanaisEDestinatarios()
    {
        var definition = CreateValidDefinition();
        definition.Actions.Add(new ActionDefinition
        {
            Name = "Escalonamento",
            Trigger = ActionTrigger.WhileActive,
            MessageTemplate = "Pendência {{Documento}}",
            Channels =
            [
                new ActionChannelDefinition { ChannelConfigurationId = Guid.NewGuid(), ChannelType = ChannelType.EvolutionApi },
                new ActionChannelDefinition { ChannelConfigurationId = Guid.NewGuid(), ChannelType = ChannelType.Email },
                new ActionChannelDefinition { ChannelConfigurationId = Guid.NewGuid(), ChannelType = ChannelType.Telegram }
            ],
            Recipients =
            [
                new RecipientDefinition { Type = RecipientType.Field, Value = "Telefone", ChannelType = ChannelType.EvolutionApi },
                new RecipientDefinition { Type = RecipientType.Field, Value = "Email", ChannelType = ChannelType.Email },
                new RecipientDefinition { Type = RecipientType.Fixed, Value = "123456", ChannelType = ChannelType.Telegram }
            ]
        });

        definition.Validate();
    }


    [Fact]
    public void DeveAceitarAgrupamentoPorRegistroEmCanalExterno()
    {
        var definition = CreateValidDefinition();
        definition.Actions[0].Channels[0].GroupingMode = NotificationGroupingMode.Individual;
        definition.Actions.Add(new ActionDefinition
        {
            Name = "WhatsApp agrupado",
            MessageTemplate = "Alteração {{Entity}}",
            Channels =
            [
                new ActionChannelDefinition
                {
                    ChannelConfigurationId = Guid.NewGuid(),
                    ChannelType = ChannelType.EvolutionApi,
                    GroupingMode = NotificationGroupingMode.ByEntity,
                    GroupField = "EntityKey",
                    GroupingWindowSeconds = 10
                }
            ],
            Recipients =
            [
                new RecipientDefinition
                {
                    Type = RecipientType.Fixed,
                    ChannelType = ChannelType.EvolutionApi,
                    Value = "5599999999999"
                }
            ]
        });

        definition.Validate();
    }

    [Fact]
    public void DeveRejeitarAgrupamentoNaNotificacaoDoWindows()
    {
        var definition = CreateValidDefinition();
        definition.Actions[0].Channels[0].GroupingMode = NotificationGroupingMode.SingleMessage;

        Assert.Throws<InvalidOperationException>(definition.Validate);
    }

    [Fact]
    public void DeveAceitarCicloRecorrenteDePendenciaComHorario()
    {
        var definition = CreateValidDefinition();
        var action = definition.Actions[0];
        action.Trigger = ActionTrigger.WhileActive;
        action.Repeat = new RepeatPolicyDefinition
        {
            Enabled = true,
            IntervalSeconds = 3600,
            MaxExecutions = 0,
            ResetOnConditionReentry = true
        };
        action.Schedule = new ActionScheduleDefinition
        {
            Enabled = true,
            StartTime = "08:00",
            EndTime = "18:00",
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Tuesday]
        };
        action.Conditions = Rules(RuleSetType.ActionCondition, RuleOperator.In, "P|PENDENTE");
        action.PersistenceConditions = Rules(RuleSetType.ActionPersistence, RuleOperator.NotIn, "X|CONCLUIDO");
        action.CompletionConditions = Rules(RuleSetType.ActionCompletion, RuleOperator.In, "X|CONCLUIDO");
        action.CancelPendingWhenConditionFails = true;

        definition.Validate();

        Assert.True(action.Schedule.IsAllowed(new DateTimeOffset(2026, 7, 27, 10, 0, 0, TimeSpan.Zero)));
        Assert.False(action.Schedule.IsAllowed(new DateTimeOffset(2026, 7, 27, 19, 0, 0, TimeSpan.Zero)));
        Assert.False(action.Schedule.IsAllowed(new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void DeveAceitarJanelaDeHorarioQueAtravesseMeiaNoite()
    {
        var schedule = new ActionScheduleDefinition
        {
            Enabled = true,
            StartTime = "22:00",
            EndTime = "06:00"
        };

        schedule.Validate("Plantão");

        Assert.True(schedule.IsAllowed(new DateTimeOffset(2026, 7, 29, 23, 30, 0, TimeSpan.Zero)));
        Assert.True(schedule.IsAllowed(new DateTimeOffset(2026, 7, 30, 5, 30, 0, TimeSpan.Zero)));
        Assert.False(schedule.IsAllowed(new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void DeveManterCompatibilidadeAoDesserializarAcaoAntigaSemCicloDePendencia()
    {
        const string json = """
        {
          "name": "Legada",
          "enabled": true,
          "trigger": "WhileActive",
          "repeat": { "enabled": true, "intervalSeconds": 60, "maxExecutions": 0 },
          "channels": [
            {
              "channelConfigurationId": "11111111-1111-1111-1111-111111111111",
              "channelType": "LocalWindows",
              "groupingMode": "Individual"
            }
          ]
        }
        """;

        var action = JsonSerializer.Deserialize<ActionDefinition>(json, FlowJson.Options);

        Assert.NotNull(action);
        Assert.NotNull(action.Schedule);
        Assert.False(action.Schedule.Enabled);
        Assert.False(action.EvaluateWhileActiveOnOpen);
        Assert.Null(action.PersistenceConditions);
        Assert.Null(action.CompletionConditions);
        Assert.False(action.Repeat.ResetOnConditionReentry);
        action.Validate();
    }

    [Fact]
    public void DeveRejeitarAutomacaoSemFontePrimaria()
    {
        var definition = CreateValidDefinition();
        definition.Sources[0].IsPrimary = false;

        Assert.Throws<InvalidOperationException>(definition.Validate);
    }

    [Fact]
    public void DeveSerializarEnumsComoTexto()
    {
        var definition = CreateValidDefinition();

        Assert.Equal(JsonValueKind.Object, definition.Sources[0].Configuration.ValueKind);

        var json = JsonSerializer.Serialize(definition, FlowJson.Options);

        Assert.Contains("\"type\": \"Csv\"", json);
        Assert.Contains("\"operator\": \"And\"", json);
        Assert.Contains("\"configuration\": {}", json);
    }

    private static RuleSetDefinition Rules(RuleSetType type, RuleOperator ruleOperator, string expectedValue) => new()
    {
        Type = type,
        Root = new RuleGroupDefinition
        {
            Rules =
            [
                new RuleDefinition
                {
                    Field = "Status",
                    Operator = ruleOperator,
                    ExpectedValue = expectedValue
                }
            ]
        }
    };

    private static AutomationDefinition CreateValidDefinition() => new()
    {
        Name = "Clientes pendentes",
        IntervalSeconds = 60,
        Sources =
        [
            new DataSourceDefinition
            {
                Alias = "clientes",
                Name = "Clientes",
                Type = SourceType.Csv,
                IsPrimary = true,
                KeyFields = ["Id"]
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
                    new RuleDefinition { Field = "Status", Operator = RuleOperator.Equal, ExpectedValue = "Pendente" }
                ]
            }
        },
        Actions =
        [
            new ActionDefinition
            {
                Name = "Alerta local",
                MessageTemplate = "Pendência",
                Channels =
                [
                    new ActionChannelDefinition
                    {
                        ChannelConfigurationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        ChannelType = ChannelType.LocalWindows
                    }
                ]
            }
        ]
    };
}

public sealed class ContactDirectoryDefinitionTests
{
    [Fact]
    public void DeveValidarCatalogoComContatosEGruposReutilizaveis()
    {
        var contact = new ContactDefinition
        {
            Name = "Gestor",
            Addresses = new Dictionary<ChannelType, List<string>>
            {
                [ChannelType.EvolutionApi] = ["+5575999999999"]
            }
        };
        var directory = new ContactDirectoryDefinition
        {
            Contacts = [contact],
            Groups =
            [
                new ContactGroupDefinition
                {
                    Id = "gestores",
                    Name = "Gestores",
                    ContactIds = [contact.Id]
                }
            ]
        };

        directory.Validate();
    }

    [Fact]
    public void DeveRejeitarGrupoComContatoInexistente()
    {
        var directory = new ContactDirectoryDefinition
        {
            Groups =
            [
                new ContactGroupDefinition
                {
                    Id = "gestores",
                    Name = "Gestores",
                    ContactIds = [Guid.NewGuid()]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => directory.Validate());
        Assert.Contains("contato inexistente", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
