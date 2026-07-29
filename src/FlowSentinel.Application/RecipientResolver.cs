using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class RecipientResolver : IRecipientResolver
{
    private readonly IContactDirectory _contactDirectory;

    public RecipientResolver(IContactDirectory contactDirectory)
    {
        _contactDirectory = contactDirectory;
    }

    public async Task<IReadOnlyCollection<ResolvedRecipient>> ResolveAsync(
        AutomationDefinition automation,
        ActionDefinition action,
        ChannelType channelType,
        EvaluationContext context,
        CancellationToken cancellationToken)
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

        var directory = await _contactDirectory.GetSnapshotAsync(cancellationToken);
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

                case RecipientType.Contact:
                    ExpandContact(result, directory, automation.Id, recipient.Value, channelType);
                    break;

                case RecipientType.Group:
                    ExpandGroup(result, directory, automation, recipient.Value, channelType);
                    break;
            }
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Address))
            .DistinctBy(x => $"{x.ChannelType}:{x.Address}", StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ExpandContact(
        ICollection<ResolvedRecipient> result,
        ContactDirectoryDefinition directory,
        Guid automationId,
        string contactReference,
        ChannelType channelType)
    {
        var contact = FindContact(directory, contactReference);
        if (contact is null || !contact.CanBeUsedBy(automationId))
        {
            return;
        }

        AddContactAddresses(result, contact, channelType);
    }

    private static void ExpandGroup(
        ICollection<ResolvedRecipient> result,
        ContactDirectoryDefinition directory,
        AutomationDefinition automation,
        string groupId,
        ChannelType channelType)
    {
        var globalGroup = directory.Groups.FirstOrDefault(x =>
            string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Name, groupId, StringComparison.OrdinalIgnoreCase));

        if (globalGroup is not null && globalGroup.CanBeUsedBy(automation.Id))
        {
            foreach (var contactId in globalGroup.ContactIds)
            {
                var contact = directory.Contacts.FirstOrDefault(x => x.Id == contactId);
                if (contact is not null && contact.CanBeUsedBy(automation.Id))
                {
                    AddContactAddresses(result, contact, channelType);
                }
            }

            foreach (var contact in globalGroup.Contacts.Where(x => x.CanBeUsedBy(automation.Id)))
            {
                AddContactAddresses(result, contact, channelType);
            }
            return;
        }

        // Compatibilidade com grupos incorporados às definições antigas de automação.
        var embeddedGroup = automation.ContactGroups.FirstOrDefault(x =>
            string.Equals(x.Id, groupId, StringComparison.OrdinalIgnoreCase));
        if (embeddedGroup is null)
        {
            return;
        }

        foreach (var contact in embeddedGroup.Contacts)
        {
            AddContactAddresses(result, contact, channelType);
        }
    }

    private static ContactDefinition? FindContact(ContactDirectoryDefinition directory, string reference)
    {
        if (Guid.TryParse(reference, out var id))
        {
            return directory.Contacts.FirstOrDefault(x => x.Id == id);
        }

        return directory.Contacts.FirstOrDefault(x =>
            string.Equals(x.Name, reference, StringComparison.OrdinalIgnoreCase));
    }

    private static void AddContactAddresses(
        ICollection<ResolvedRecipient> result,
        ContactDefinition contact,
        ChannelType channelType)
    {
        if (!contact.Addresses.TryGetValue(channelType, out var addresses))
        {
            return;
        }

        foreach (var address in addresses)
        {
            Add(result, channelType, address, contact.Name);
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

        foreach (var item in address.Split([';', ',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
