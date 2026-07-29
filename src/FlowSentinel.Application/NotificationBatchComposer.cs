using System.Text;
using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public static class NotificationBatchComposer
{
    public static string ResolveEntityKey(
        IReadOnlyDictionary<string, string?> fields,
        string? configuredField = null)
    {
        var candidates = new[]
        {
            configuredField,
            "EntityKey",
            "CompanyKey",
            "Entity",
            "Company",
            "Code",
            "record.key"
        };

        foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (fields.TryGetValue(candidate!, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    public static string ResolveEntityDisplay(IReadOnlyDictionary<string, string?> fields)
    {
        foreach (var field in new[] { "Entity", "Company", "Code", "EntityKey", "CompanyKey" })
        {
            if (fields.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "registro";
    }

    public static (string Subject, string Message) Compose(
        string automationName,
        NotificationGroupingMode mode,
        IReadOnlyList<DeliveryStoreItem> deliveries)
    {
        if (deliveries.Count == 0)
        {
            return (automationName, string.Empty);
        }

        if (deliveries.Count == 1 || mode == NotificationGroupingMode.Individual)
        {
            return (deliveries[0].Subject, deliveries[0].Message);
        }

        var uniqueMessages = deliveries
            .Select(x => x.Message.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var entity = ResolveEntityDisplay(deliveries[0].Fields);
        var subject = mode == NotificationGroupingMode.ByEntity
            ? $"{uniqueMessages.Length} alterações — {entity}"
            : $"{uniqueMessages.Length} alterações — {automationName}";

        var builder = new StringBuilder();
        if (mode == NotificationGroupingMode.ByEntity)
        {
            builder.Append("O FlowSentinel identificou ")
                .Append(uniqueMessages.Length)
                .Append(uniqueMessages.Length == 1 ? " alteração em " : " alterações em ")
                .Append(entity)
                .AppendLine(":")
                .AppendLine();
        }
        else
        {
            builder.Append("O FlowSentinel identificou ")
                .Append(uniqueMessages.Length)
                .Append(uniqueMessages.Length == 1 ? " alteração" : " alterações")
                .Append(" no monitoramento '")
                .Append(automationName)
                .AppendLine("':")
                .AppendLine();
        }

        for (var index = 0; index < uniqueMessages.Length; index++)
        {
            builder.Append(index + 1)
                .Append(". ")
                .AppendLine(uniqueMessages[index]);
        }

        return (subject, builder.ToString().TrimEnd());
    }
}
