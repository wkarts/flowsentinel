using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Application.Tests;

public sealed class RecipientResolverTests
{
    [Fact]
    public async Task DeveResolverContatoEGrupoDoCatalogoRespeitandoCanal()
    {
        var automationId = Guid.NewGuid();
        var contactA = Contact("Gestor", ChannelType.EvolutionApi, "+5575999999999");
        var contactB = Contact("Financeiro", ChannelType.Email, "financeiro@empresa.com.br");
        var directory = new ContactDirectoryDefinition
        {
            Contacts = [contactA, contactB],
            Groups =
            [
                new ContactGroupDefinition
                {
                    Id = "gestores",
                    Name = "Gestores",
                    ContactIds = [contactA.Id, contactB.Id]
                }
            ]
        };
        var resolver = new RecipientResolver(new MemoryContactDirectory(directory));
        var automation = Automation(automationId);
        var action = new ActionDefinition
        {
            Name = "Avisar",
            Channels =
            [
                new ActionChannelDefinition
                {
                    ChannelConfigurationId = Guid.NewGuid(),
                    ChannelType = ChannelType.EvolutionApi
                }
            ],
            Recipients =
            [
                new RecipientDefinition
                {
                    Type = RecipientType.Group,
                    Value = "gestores",
                    ChannelType = ChannelType.EvolutionApi
                }
            ]
        };

        var result = await resolver.ResolveAsync(
            automation,
            action,
            ChannelType.EvolutionApi,
            Context(automation),
            CancellationToken.None);

        var recipient = Assert.Single(result);
        Assert.Equal("+5575999999999", recipient.Address);
        Assert.Equal("Gestor", recipient.DisplayName);
    }

    [Fact]
    public async Task NaoDeveResolverContatoRestritoParaAutomacaoNaoAutorizada()
    {
        var automation = Automation(Guid.NewGuid());
        var contact = Contact("Diretoria", ChannelType.Email, "diretoria@empresa.com.br");
        contact.AccessScope = ContactAccessScope.SelectedAutomations;
        contact.AllowedAutomationIds = [Guid.NewGuid()];
        var resolver = new RecipientResolver(new MemoryContactDirectory(new ContactDirectoryDefinition
        {
            Contacts = [contact]
        }));
        var action = new ActionDefinition
        {
            Name = "Avisar",
            Channels =
            [
                new ActionChannelDefinition
                {
                    ChannelConfigurationId = Guid.NewGuid(),
                    ChannelType = ChannelType.Email
                }
            ],
            Recipients =
            [
                new RecipientDefinition
                {
                    Type = RecipientType.Contact,
                    Value = contact.Id.ToString("D"),
                    ChannelType = ChannelType.Email
                }
            ]
        };

        var result = await resolver.ResolveAsync(
            automation,
            action,
            ChannelType.Email,
            Context(automation),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task DeveManterNotificacaoWindowsSempreLocal()
    {
        var automation = Automation(Guid.NewGuid());
        var resolver = new RecipientResolver(new MemoryContactDirectory(new ContactDirectoryDefinition()));
        var action = new ActionDefinition
        {
            Name = "Avisar",
            Channels =
            [
                new ActionChannelDefinition
                {
                    ChannelConfigurationId = Guid.NewGuid(),
                    ChannelType = ChannelType.LocalWindows
                }
            ]
        };

        var result = await resolver.ResolveAsync(
            automation,
            action,
            ChannelType.LocalWindows,
            Context(automation),
            CancellationToken.None);

        var recipient = Assert.Single(result);
        Assert.Equal("local", recipient.Address);
        Assert.Equal(ChannelType.LocalWindows, recipient.ChannelType);
    }

    private static ContactDefinition Contact(string name, ChannelType channel, string address) => new()
    {
        Name = name,
        Addresses = new Dictionary<ChannelType, List<string>>
        {
            [channel] = [address]
        }
    };

    private static AutomationDefinition Automation(Guid id) => new()
    {
        Id = id,
        Name = "Teste",
        Sources =
        [
            new DataSourceDefinition
            {
                Name = "Fonte",
                Alias = "primary",
                Type = SourceType.Csv,
                IsPrimary = true,
                KeyFields = ["Id"]
            }
        ]
    };

    private static EvaluationContext Context(AutomationDefinition automation) => new()
    {
        Automation = automation,
        RecordKey = "1",
        Fields = new Dictionary<string, string?>()
    };

    private sealed class MemoryContactDirectory : IContactDirectory
    {
        private ContactDirectoryDefinition _definition;

        internal MemoryContactDirectory(ContactDirectoryDefinition definition)
        {
            _definition = definition;
        }

        public Task<ContactDirectoryDefinition> GetSnapshotAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_definition);

        public Task SaveAsync(ContactDirectoryDefinition definition, CancellationToken cancellationToken)
        {
            _definition = definition;
            return Task.CompletedTask;
        }
    }
}
