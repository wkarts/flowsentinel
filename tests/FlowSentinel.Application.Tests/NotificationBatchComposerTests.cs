using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Application.Tests;

public sealed class NotificationBatchComposerTests
{
    [Fact]
    public void DeveManterMensagemIndividualSemAlteracao()
    {
        var delivery = Delivery("Empresa A", "Situação alterada.");

        var result = NotificationBatchComposer.Compose(
            "Monitoramento",
            NotificationGroupingMode.Individual,
            [delivery]);

        Assert.Equal(delivery.Subject, result.Subject);
        Assert.Equal(delivery.Message, result.Message);
    }

    [Fact]
    public void DeveAgruparVariasAlteracoesPorRegistro()
    {
        var deliveries = new[]
        {
            Delivery("Empresa A", "Janeiro: vazio para X."),
            Delivery("Empresa A", "Fevereiro: vazio para X.")
        };

        var result = NotificationBatchComposer.Compose(
            "Conferência",
            NotificationGroupingMode.ByEntity,
            deliveries);

        Assert.Contains("2 alterações", result.Subject);
        Assert.Contains("Empresa A", result.Subject);
        Assert.Contains("1. Janeiro", result.Message);
        Assert.Contains("2. Fevereiro", result.Message);
    }

    [Fact]
    public void DeveResolverChaveGenericaComFallbackLegado()
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyKey"] = "270",
            ["Company"] = "Empresa A"
        };

        var key = NotificationBatchComposer.ResolveEntityKey(fields, "EntityKey");

        Assert.Equal("270", key);
    }

    private static DeliveryStoreItem Delivery(string entity, string message) => new()
    {
        Id = Guid.NewGuid(),
        OccurrenceId = Guid.NewGuid(),
        AutomationId = Guid.NewGuid(),
        ActionId = Guid.NewGuid(),
        ChannelConfigurationId = Guid.NewGuid(),
        ChannelType = ChannelType.EvolutionApi,
        Recipient = "5599999999999",
        Subject = $"Alteração — {entity}",
        Message = message,
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        ExecutionNumber = 1,
        Status = DeliveryStatus.Pending,
        DueAt = DateTimeOffset.Now,
        Fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["EntityKey"] = entity,
            ["Entity"] = entity
        }
    };
}
