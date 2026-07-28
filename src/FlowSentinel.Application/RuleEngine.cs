using System.Globalization;
using System.Text.RegularExpressions;
using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class RuleEngine : IRuleEngine
{
    public bool Evaluate(RuleSetDefinition? ruleSet, EvaluationContext context, bool defaultValue = true)
    {
        if (ruleSet is null)
        {
            return defaultValue;
        }

        return EvaluateGroup(ruleSet.Root, context);
    }

    private static bool EvaluateGroup(RuleGroupDefinition group, EvaluationContext context)
    {
        var results = new List<bool>(group.Rules.Count + group.Groups.Count);
        results.AddRange(group.Rules.Select(rule => EvaluateRule(rule, context)));
        results.AddRange(group.Groups.Select(child => EvaluateGroup(child, context)));

        var result = results.Count == 0 || (group.Operator switch
        {
            LogicalOperator.And => results.All(x => x),
            LogicalOperator.Or => results.Any(x => x),
            _ => false
        });

        return group.Negate ? !result : result;
    }

    private static bool EvaluateRule(RuleDefinition rule, EvaluationContext context)
    {
        context.Fields.TryGetValue(rule.Field, out var actual);
        context.PreviousFields.TryGetValue(rule.Field, out var previous);
        var expected = ResolveExpected(rule, context);
        var comparison = rule.CaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        return rule.Operator switch
        {
            RuleOperator.Equal => string.Equals(actual?.Trim(), expected?.Trim(), comparison),
            RuleOperator.NotEqual => !string.Equals(actual?.Trim(), expected?.Trim(), comparison),
            RuleOperator.Contains => actual?.Contains(expected ?? string.Empty, comparison) == true,
            RuleOperator.NotContains => actual?.Contains(expected ?? string.Empty, comparison) != true,
            RuleOperator.StartsWith => actual?.StartsWith(expected ?? string.Empty, comparison) == true,
            RuleOperator.EndsWith => actual?.EndsWith(expected ?? string.Empty, comparison) == true,
            RuleOperator.IsEmpty => string.IsNullOrWhiteSpace(actual),
            RuleOperator.IsNotEmpty => !string.IsNullOrWhiteSpace(actual),
            RuleOperator.Exists => context.Fields.ContainsKey(rule.Field),
            RuleOperator.NotExists => !context.Fields.ContainsKey(rule.Field),
            RuleOperator.In => SplitValues(expected).Any(x => string.Equals(actual?.Trim(), x, comparison)),
            RuleOperator.NotIn => SplitValues(expected).All(x => !string.Equals(actual?.Trim(), x, comparison)),
            RuleOperator.Regex => actual is not null && expected is not null && Regex.IsMatch(actual, expected, rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)),
            RuleOperator.GreaterThan => Compare(actual, expected, rule.Culture) > 0,
            RuleOperator.GreaterThanOrEqual => Compare(actual, expected, rule.Culture) >= 0,
            RuleOperator.LessThan => Compare(actual, expected, rule.Culture) < 0,
            RuleOperator.LessThanOrEqual => Compare(actual, expected, rule.Culture) <= 0,
            RuleOperator.Changed => context.PreviousFields.ContainsKey(rule.Field) && !string.Equals(actual, previous, comparison),
            RuleOperator.Unchanged => context.PreviousFields.ContainsKey(rule.Field) && string.Equals(actual, previous, comparison),
            RuleOperator.ChangedFromTo => EvaluateChangedFromTo(previous, actual, expected, comparison),
            _ => false
        };
    }

    private static string? ResolveExpected(RuleDefinition rule, EvaluationContext context)
    {
        if (!string.IsNullOrWhiteSpace(rule.ExpectedField))
        {
            return context.Fields.GetValueOrDefault(rule.ExpectedField);
        }

        return rule.ExpectedValue switch
        {
            "{{today}}" => context.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "{{now}}" => context.Now.ToString("O", CultureInfo.InvariantCulture),
            _ => rule.ExpectedValue
        };
    }

    private static IReadOnlyCollection<string> SplitValues(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(['|', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Compare(string? left, string? right, string? cultureName)
    {
        if (left is null && right is null)
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var culture = GetCulture(cultureName);
        if (decimal.TryParse(left, NumberStyles.Any, culture, out var leftNumber) &&
            decimal.TryParse(right, NumberStyles.Any, culture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (DateTimeOffset.TryParse(left, culture, DateTimeStyles.AssumeLocal, out var leftDate) &&
            DateTimeOffset.TryParse(right, culture, DateTimeStyles.AssumeLocal, out var rightDate))
        {
            return leftDate.CompareTo(rightDate);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static CultureInfo GetCulture(string? cultureName)
    {
        try
        {
            return string.IsNullOrWhiteSpace(cultureName)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static bool EvaluateChangedFromTo(string? previous, string? actual, string? expected, StringComparison comparison)
    {
        var values = SplitValues(expected).ToArray();
        return values.Length == 2 &&
               string.Equals(previous?.Trim(), values[0], comparison) &&
               string.Equals(actual?.Trim(), values[1], comparison);
    }
}
