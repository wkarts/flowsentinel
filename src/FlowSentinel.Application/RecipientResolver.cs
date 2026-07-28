using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class RecipientResolver : IRecipientResolver
{
    public IReadOnlyCollection<ResolvedRecipient> Resolve(
        AutomationDefinition automation,
        ActionDefinition action,
        ChannelType channelType,
        EvaluationContext context)
    {
        if (channelType == ChannelType.LocalWindows)
        {
            return
            [
                new ResolvedRecipient
                {
                    ChannelType = channelType,
                    Address = "local",
                    DisplayName = Environment.MachineName
                }
            ];
        }

        var result = new List<ResolvedRecipient>();
        foreach (var recipient in action.Recipients)
        {
            if (recipient.ChannelType.HasValue && recipient.ChannelType.Value != channelType)
            {
                continue;
            }

            switch (recipient.Type)
            {
                case RecipientType.Fixed:
                    Add(result, channelType, recipient.Value, recipient.DisplayName);
                    break;

                case RecipientType.Field:
                    Add(result, channelType, context.Fields.GetValueOrDefault(recipient.Value), recipient.DisplayName);
                    break;

                case RecipientType.Group:
                    ExpandGroup(result, automation, recipient.Value, channelType);
                    break;
            }
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Address))
            .DistinctBy(x => $"{x.ChannelType}:{x.Address}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ExpandGroup(
        ICollection<ResolvedRecipient> result,
        AutomationDefinition automation,
        string groupId,
        ChannelType channelType)
    {
        var group = automation.ContactGroups.FirstOrDefault(x =>
            string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase));

        if (group is null)
        {
            return;
        }

        foreach (var contact in group.Contacts)
        {
            if (!contact.Addresses.TryGetValue(channelType, out var addresses))
            {
                continue;
            }

            foreach (var address in addresses)
            {
                Add(result, channelType, address, contact.Name);
            }
        }
    }

    private static void Add(
        ICollection<ResolvedRecipient> result,
        ChannelType channelType,
        string? address,
        string displayName)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        foreach (var item in address.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result.Add(new ResolvedRecipient
            {
                ChannelType = channelType,
                Address = item,
                DisplayName = displayName
            });
        }
    }
}
