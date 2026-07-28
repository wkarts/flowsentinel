using System.Globalization;
using System.Text.RegularExpressions;
using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed partial class TemplateRenderer : ITemplateRenderer
{
    public string Render(string template, EvaluationContext context)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return TokenRegex().Replace(template, match =>
        {
            var token = match.Groups[1].Value.Trim();
            return token.ToLowerInvariant() switch
            {
                "automation.name" => context.Automation.Name,
                "automation.description" => context.Automation.Description,
                "record.key" => context.RecordKey,
                "now" => context.Now.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("pt-BR")),
                "today" => context.Now.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR")),
                _ => context.Fields.GetValueOrDefault(token) ?? string.Empty
            };
        });
    }

    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();
}
