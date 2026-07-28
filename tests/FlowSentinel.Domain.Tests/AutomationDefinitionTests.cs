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
    public void DeveRejeitarAutomacaoSemFontePrimaria()
    {
        var definition = CreateValidDefinition();
        definition.Sources[0].IsPrimary = false;

        Assert.Throws<InvalidOperationException>(definition.Validate);
    }

    [Fact]
    public void DeveSerializarEnumsComoTexto()
    {
        var json = JsonSerializer.Serialize(CreateValidDefinition(), FlowJson.Options);
        Assert.Contains("\"type\": \"Csv\"", json);
        Assert.Contains("\"operator\": \"And\"", json);
    }

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
