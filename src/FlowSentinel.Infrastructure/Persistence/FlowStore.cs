using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Infrastructure.Persistence;

internal sealed class FlowStore : IFlowStore
{
    public static readonly Guid LocalChannelId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IDbContextFactory<FlowSentinelDbContext> _factory;
    private readonly ILogger<FlowStore> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly SemaphoreSlim _claimLock = new(1, 1);
    private bool _initialized;

    public FlowStore(
        IDbContextFactory<FlowSentinelDbContext> factory,
        ILogger<FlowStore> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var context = await _factory.CreateDbContextAsync(cancellationToken);
            await context.Database.EnsureCreatedAsync(cancellationToken);
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);
            await SeedAsync(context, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<IReadOnlyCollection<AutomationStoreItem>> GetAutomationsAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        return await context.Automations
            .AsNoTracking()
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .Select(x => new AutomationStoreItem
            {
                Id = x.Id,
                Name = x.Name,
                Enabled = x.Enabled,
                IntervalSeconds = x.IntervalSeconds,
                LastRunAt = x.LastRunAt,
                NextRunAt = x.NextRunAt,
                LastError = x.LastError
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AutomationStoreItem>> GetDueAutomationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        return await context.Automations
            .AsNoTracking()
            .Where(x => x.Enabled && x.NextRunAt <= now)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.NextRunAt)
            .Select(x => new AutomationStoreItem
            {
                Id = x.Id,
                Name = x.Name,
                Enabled = x.Enabled,
                IntervalSeconds = x.IntervalSeconds,
                LastRunAt = x.LastRunAt,
                NextRunAt = x.NextRunAt,
                LastError = x.LastError
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AutomationDefinition?> GetAutomationDefinitionAsync(
        Guid automationId,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var json = await context.Automations
            .AsNoTracking()
            .Where(x => x.Id == automationId)
            .Select(x => x.DefinitionJson)
            .SingleOrDefaultAsync(cancellationToken);

        return json is null
            ? null
            : JsonSerializer.Deserialize<AutomationDefinition>(json, FlowJson.Options);
    }

    public async Task SaveAutomationAsync(
        AutomationDefinition definition,
        CancellationToken cancellationToken)
    {
        definition.Validate();
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Automations.SingleOrDefaultAsync(x => x.Id == definition.Id, cancellationToken);
        var now = DateTimeOffset.Now;
        var json = JsonSerializer.Serialize(definition, FlowJson.Options);

        if (entity is null)
        {
            entity = new AutomationEntity
            {
                Id = definition.Id,
                CreatedAt = now,
                NextRunAt = now
            };
            context.Automations.Add(entity);
        }

        entity.Name = definition.Name;
        entity.Enabled = definition.Enabled;
        entity.IntervalSeconds = definition.IntervalSeconds;
        entity.Priority = definition.Priority;
        entity.DefinitionJson = json;
        entity.UpdatedAt = now;
        if (!definition.Enabled)
        {
            entity.NextRunAt = now.AddYears(10);
        }
        else if (entity.NextRunAt > now.AddYears(1))
        {
            entity.NextRunAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAutomationAsync(Guid automationId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var deliveryIds = context.Deliveries.Where(x => x.AutomationId == automationId);
        var occurrenceIds = context.Occurrences.Where(x => x.AutomationId == automationId);
        context.Deliveries.RemoveRange(deliveryIds);
        context.Occurrences.RemoveRange(occurrenceIds);
        var automation = await context.Automations.SingleOrDefaultAsync(x => x.Id == automationId, cancellationToken);
        if (automation is not null)
        {
            context.Automations.Remove(automation);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAutomationExecutionAsync(
        Guid automationId,
        DateTimeOffset nextRunAt,
        string? error,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Automations.SingleOrDefaultAsync(x => x.Id == automationId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastRunAt = DateTimeOffset.Now;
        entity.NextRunAt = nextRunAt;
        entity.LastError = Truncate(error, 4000);
        entity.UpdatedAt = DateTimeOffset.Now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<OccurrenceStoreItem?> GetOpenOccurrenceAsync(
        Guid automationId,
        string recordKey,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Occurrences
            .AsNoTracking()
            .Where(x => x.AutomationId == automationId &&
                        x.RecordKey == recordKey &&
                        (x.Status == OccurrenceStatus.New ||
                         x.Status == OccurrenceStatus.Active ||
                         x.Status == OccurrenceStatus.Suspended))
            .OrderByDescending(x => x.OpenedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyCollection<OccurrenceStoreItem>> GetOpenOccurrencesAsync(
        Guid automationId,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Occurrences
            .AsNoTracking()
            .Where(x => x.AutomationId == automationId &&
                        (x.Status == OccurrenceStatus.New ||
                         x.Status == OccurrenceStatus.Active ||
                         x.Status == OccurrenceStatus.Suspended))
            .ToArrayAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task CreateOccurrenceAsync(
        OccurrenceStoreItem occurrence,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.Occurrences.Add(Map(occurrence));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOccurrenceAsync(
        OccurrenceStoreItem occurrence,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Occurrences.SingleAsync(x => x.Id == occurrence.Id, cancellationToken);
        entity.Status = occurrence.Status;
        entity.LastEvaluatedAt = occurrence.LastEvaluatedAt;
        entity.ResolvedAt = occurrence.ResolvedAt;
        entity.SnapshotJson = JsonSerializer.Serialize(occurrence.Snapshot, FlowJson.Options);
        entity.Fingerprint = occurrence.Fingerprint;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ActionScheduleState> GetActionScheduleStateAsync(
        Guid occurrenceId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var query = context.Deliveries.AsNoTracking()
            .Where(x => x.OccurrenceId == occurrenceId && x.ActionId == actionId);

        var executionCount = await query.Select(x => (int?)x.ExecutionNumber).MaxAsync(cancellationToken) ?? 0;
        var lastScheduled = await query.Select(x => (DateTimeOffset?)x.CreatedAt).MaxAsync(cancellationToken);
        return new ActionScheduleState
        {
            ExecutionCount = executionCount,
            LastScheduledAt = lastScheduled
        };
    }

    public async Task AddDeliveriesAsync(
        IReadOnlyCollection<DeliveryStoreItem> deliveries,
        CancellationToken cancellationToken)
    {
        if (deliveries.Count == 0)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var keys = deliveries.Select(x => x.IdempotencyKey).Distinct(StringComparer.Ordinal).ToArray();
        var existing = await context.Deliveries
            .AsNoTracking()
            .Where(x => keys.Contains(x.IdempotencyKey))
            .Select(x => x.IdempotencyKey)
            .ToArrayAsync(cancellationToken);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var delivery in deliveries.Where(x => !existingSet.Contains(x.IdempotencyKey)))
        {
            context.Deliveries.Add(Map(delivery));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelPendingDeliveriesAsync(Guid occurrenceId, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var deliveries = await context.Deliveries
            .Where(x => x.OccurrenceId == occurrenceId &&
                        (x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.RetryScheduled))
            .ToArrayAsync(cancellationToken);
        foreach (var delivery in deliveries)
        {
            delivery.Status = DeliveryStatus.Cancelled;
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<DeliveryStoreItem>> ClaimDueDeliveriesAsync(
        DateTimeOffset now,
        int take,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await _claimLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = await _factory.CreateDbContextAsync(cancellationToken);
            var entities = await context.Deliveries
                .Where(x => (x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.RetryScheduled) &&
                            x.DueAt <= now)
                .OrderBy(x => x.DueAt)
                .Take(take)
                .ToArrayAsync(cancellationToken);

            foreach (var entity in entities)
            {
                entity.Status = DeliveryStatus.Processing;
                entity.AttemptCount++;
            }
            await context.SaveChangesAsync(cancellationToken);
            return entities.Select(Map).ToArray();
        }
        finally
        {
            _claimLock.Release();
        }
    }

    public async Task CompleteDeliveryAsync(
        Guid deliveryId,
        DeliveryResult result,
        DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Deliveries.SingleOrDefaultAsync(x => x.Id == deliveryId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.LastError = Truncate(result.Error, 4000);
        entity.ExternalMessageId = Truncate(result.ExternalMessageId, 500);

        if (result.Success)
        {
            entity.Status = DeliveryStatus.Sent;
            entity.SentAt = DateTimeOffset.Now;
        }
        else if (result.IsTransient && nextAttemptAt.HasValue)
        {
            entity.Status = DeliveryStatus.RetryScheduled;
            entity.DueAt = nextAttemptAt.Value;
        }
        else
        {
            entity.Status = DeliveryStatus.Failed;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ChannelConfiguration?> GetChannelConfigurationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ChannelConfigurations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyCollection<ChannelConfiguration>> GetChannelConfigurationsAsync(
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entities = await context.ChannelConfigurations.AsNoTracking()
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task SaveChannelConfigurationAsync(
        ChannelConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (configuration.Id == Guid.Empty || string.IsNullOrWhiteSpace(configuration.Name))
        {
            throw new InvalidOperationException("A configuração do canal precisa possuir ID e nome.");
        }

        JsonDocument.Parse(configuration.SettingsJson).Dispose();
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ChannelConfigurations
            .SingleOrDefaultAsync(x => x.Id == configuration.Id, cancellationToken);
        var now = DateTimeOffset.Now;
        if (entity is null)
        {
            entity = new ChannelConfigurationEntity
            {
                Id = configuration.Id,
                CreatedAt = now
            };
            context.ChannelConfigurations.Add(entity);
        }

        entity.Name = configuration.Name;
        entity.Type = configuration.Type;
        entity.Enabled = configuration.Enabled;
        entity.SettingsJson = configuration.SettingsJson;
        entity.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteChannelConfigurationAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == LocalChannelId)
        {
            throw new InvalidOperationException("O canal local padrão não pode ser removido.");
        }

        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ChannelConfigurations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is not null)
        {
            context.ChannelConfigurations.Remove(entity);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        return new DashboardSnapshot
        {
            EnabledAutomations = await context.Automations.CountAsync(x => x.Enabled, cancellationToken),
            ActiveOccurrences = await context.Occurrences.CountAsync(
                x => x.Status == OccurrenceStatus.New || x.Status == OccurrenceStatus.Active || x.Status == OccurrenceStatus.Suspended,
                cancellationToken),
            PendingDeliveries = await context.Deliveries.CountAsync(
                x => x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.RetryScheduled || x.Status == DeliveryStatus.Processing,
                cancellationToken),
            FailedDeliveries = await context.Deliveries.CountAsync(x => x.Status == DeliveryStatus.Failed, cancellationToken),
            LastExecutionAt = await context.Automations.Select(x => x.LastRunAt).MaxAsync(cancellationToken)
        };
    }

    private async Task SeedAsync(FlowSentinelDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.ChannelConfigurations.AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.Now;
            context.ChannelConfigurations.Add(new ChannelConfigurationEntity
            {
                Id = LocalChannelId,
                Name = "Notificação local do Windows",
                Type = ChannelType.LocalWindows,
                Enabled = true,
                SettingsJson = "{}",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        if (!await context.Automations.AnyAsync(cancellationToken))
        {
            var samplePath = Path.Combine(AppContext.BaseDirectory, "examples", "automation-clientes.json");
            if (File.Exists(samplePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(samplePath, cancellationToken);
                    var definition = JsonSerializer.Deserialize<AutomationDefinition>(json, FlowJson.Options);
                    if (definition is not null)
                    {
                        definition.Validate();
                        var now = DateTimeOffset.Now;
                        context.Automations.Add(new AutomationEntity
                        {
                            Id = definition.Id,
                            Name = definition.Name,
                            Enabled = definition.Enabled,
                            IntervalSeconds = definition.IntervalSeconds,
                            Priority = definition.Priority,
                            DefinitionJson = JsonSerializer.Serialize(definition, FlowJson.Options),
                            NextRunAt = now,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Não foi possível importar a automação de exemplo.");
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static OccurrenceStoreItem Map(OccurrenceEntity entity) => new()
    {
        Id = entity.Id,
        AutomationId = entity.AutomationId,
        RecordKey = entity.RecordKey,
        Status = entity.Status,
        OpenedAt = entity.OpenedAt,
        LastEvaluatedAt = entity.LastEvaluatedAt,
        ResolvedAt = entity.ResolvedAt,
        Snapshot = JsonSerializer.Deserialize<Dictionary<string, string?>>(entity.SnapshotJson, FlowJson.Options)
                   ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
        Fingerprint = entity.Fingerprint
    };

    private static OccurrenceEntity Map(OccurrenceStoreItem item) => new()
    {
        Id = item.Id,
        AutomationId = item.AutomationId,
        RecordKey = item.RecordKey,
        Status = item.Status,
        OpenedAt = item.OpenedAt,
        LastEvaluatedAt = item.LastEvaluatedAt,
        ResolvedAt = item.ResolvedAt,
        SnapshotJson = JsonSerializer.Serialize(item.Snapshot, FlowJson.Options),
        Fingerprint = item.Fingerprint
    };

    private static DeliveryStoreItem Map(DeliveryEntity entity) => new()
    {
        Id = entity.Id,
        OccurrenceId = entity.OccurrenceId,
        AutomationId = entity.AutomationId,
        ActionId = entity.ActionId,
        ChannelConfigurationId = entity.ChannelConfigurationId,
        ChannelType = entity.ChannelType,
        Recipient = entity.Recipient,
        Subject = entity.Subject,
        Message = entity.Message,
        IdempotencyKey = entity.IdempotencyKey,
        ExecutionNumber = entity.ExecutionNumber,
        CreatedAt = entity.CreatedAt,
        Status = entity.Status,
        AttemptCount = entity.AttemptCount,
        DueAt = entity.DueAt,
        SentAt = entity.SentAt,
        ExternalMessageId = entity.ExternalMessageId,
        LastError = entity.LastError,
        Fields = JsonSerializer.Deserialize<Dictionary<string, string?>>(entity.FieldsJson, FlowJson.Options)
                 ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
    };

    private static DeliveryEntity Map(DeliveryStoreItem item) => new()
    {
        Id = item.Id,
        OccurrenceId = item.OccurrenceId,
        AutomationId = item.AutomationId,
        ActionId = item.ActionId,
        ChannelConfigurationId = item.ChannelConfigurationId,
        ChannelType = item.ChannelType,
        Recipient = item.Recipient,
        Subject = item.Subject,
        Message = item.Message,
        IdempotencyKey = item.IdempotencyKey,
        ExecutionNumber = item.ExecutionNumber,
        Status = item.Status,
        AttemptCount = item.AttemptCount,
        CreatedAt = item.CreatedAt,
        DueAt = item.DueAt,
        SentAt = item.SentAt,
        ExternalMessageId = item.ExternalMessageId,
        LastError = item.LastError,
        FieldsJson = JsonSerializer.Serialize(item.Fields, FlowJson.Options)
    };

    private static ChannelConfiguration Map(ChannelConfigurationEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Type = entity.Type,
        Enabled = entity.Enabled,
        SettingsJson = entity.SettingsJson
    };

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength
            ? value
            : value[..maxLength];
}
