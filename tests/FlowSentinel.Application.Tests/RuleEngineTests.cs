using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Application.Tests;

public sealed class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    [Fact]
    public void DeveAvaliarGrupoAndComMultiplosCriterios()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["Status"] = "Pendente",
            ["Valor"] = "150,00"
        });
        var rules = CreateRules(LogicalOperator.And,
            new RuleDefinition { Field = "Status", Operator = RuleOperator.Equal, ExpectedValue = "Pendente" },
            new RuleDefinition { Field = "Valor", Operator = RuleOperator.GreaterThan, ExpectedValue = "100,00" });

        Assert.True(_engine.Evaluate(rules, context));
    }

    [Fact]
    public void DeveAvaliarGruposAninhadosComOr()
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["Status"] = "Atrasado",
            ["Cancelado"] = "Não"
        });
        var rules = new RuleSetDefinition
        {
            Type = RuleSetType.Entry,
            Root = new RuleGroupDefinition
            {
                Operator = LogicalOperator.And,
                Rules =
                [
                    new RuleDefinition { Field = "Cancelado", Operator = RuleOperator.Equal, ExpectedValue = "Não" }
                ],
                Groups =
                [
                    new RuleGroupDefinition
                    {
                        Operator = LogicalOperator.Or,
                        Rules =
                        [
                            new RuleDefinition { Field = "Status", Operator = RuleOperator.Equal, ExpectedValue = "Pendente" },
                            new RuleDefinition { Field = "Status", Operator = RuleOperator.Equal, ExpectedValue = "Atrasado" }
                        ]
                    }
                ]
            }
        };

        Assert.True(_engine.Evaluate(rules, context));
    }

    [Fact]
    public void DeveIdentificarMudancaDeEstado()
    {
        var context = new EvaluationContext
        {
            Automation = new AutomationDefinition { Name = "Teste" },
            RecordKey = "1",
            Fields = new Dictionary<string, string?> { ["Status"] = "Pago" },
            PreviousFields = new Dictionary<string, string?> { ["Status"] = "Pendente" },
            Now = DateTimeOffset.Now
        };
        var rules = CreateRules(LogicalOperator.And,
            new RuleDefinition
            {
                Field = "Status",
                Operator = RuleOperator.ChangedFromTo,
                ExpectedValue = "Pendente|Pago"
            });

        Assert.True(_engine.Evaluate(rules, context));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("7")]
    [InlineData("AGUARDANDO DOCUMENTAÇÃO")]
    [InlineData("Em análise")]
    public void DeveAceitarLetraNumeroPalavraOuFraseComoValorDePendencia(string actual)
    {
        var context = CreateContext(new Dictionary<string, string?>
        {
            ["Status"] = actual
        });
        var rules = CreateRules(LogicalOperator.And,
            new RuleDefinition
            {
                Field = "Status",
                Operator = RuleOperator.In,
                ExpectedValue = "A|7|AGUARDANDO DOCUMENTAÇÃO|EM ANÁLISE"
            });

        Assert.True(_engine.Evaluate(rules, context));
    }

    private static RuleSetDefinition CreateRules(LogicalOperator logicalOperator, params RuleDefinition[] rules) => new()
    {
        Type = RuleSetType.Entry,
        Root = new RuleGroupDefinition
        {
            Operator = logicalOperator,
            Rules = [.. rules]
        }
    };

    private static EvaluationContext CreateContext(IReadOnlyDictionary<string, string?> fields) => new()
    {
        Automation = new AutomationDefinition { Name = "Teste" },
        RecordKey = "1",
        Fields = fields,
        PreviousFields = new Dictionary<string, string?>(),
        Now = new DateTimeOffset(2026, 7, 28, 10, 0, 0, TimeSpan.FromHours(-3))
    };
}
