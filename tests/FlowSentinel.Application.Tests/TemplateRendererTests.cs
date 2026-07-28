using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Application.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void DeveSubstituirCamposEVariaveisDoSistema()
    {
        var renderer = new TemplateRenderer();
        var context = new EvaluationContext
        {
            Automation = new AutomationDefinition { Name = "Cobranças" },
            RecordKey = "DOC-10",
            Fields = new Dictionary<string, string?>
            {
                ["Cliente"] = "Empresa A",
                ["Valor"] = "150,00"
            },
            PreviousFields = new Dictionary<string, string?>(),
            Now = new DateTimeOffset(2026, 7, 28, 10, 30, 0, TimeSpan.FromHours(-3))
        };

        var text = renderer.Render("{{Cliente}} | {{automation.name}} | {{record.key}} | {{today}}", context);

        Assert.Contains("Empresa A", text);
        Assert.Contains("Cobranças", text);
        Assert.Contains("DOC-10", text);
        Assert.Contains("28/07/2026", text);
    }
}
