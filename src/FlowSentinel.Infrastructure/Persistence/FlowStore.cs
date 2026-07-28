using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Infrastructure.Persistence;

internal sealed class FlowStore : IFlowStore
{
    private const int CurrentStorageVersion = 2;

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
            await NormalizeStoredDateTimesAsync(context, cancellationToken);
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
        var entities = await context.Automations
            .AsNoTracking()
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToArrayAsync(cancellationToken);

        return entities.Select(Map).ToArray();
    }

    public async Task<IReadOnlyCollection<AutomationStoreItem>> GetDueAutomationsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var nowUtc = now.UtcDateTime;
        var entities = await context.Automations
            .AsNoTracking()
            .Where(x => x.Enabled && x.NextRunAt <= nowUtc)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.NextRunAt)
            .ToArrayAsync(cancellationToken);

        return entities.Select(Map).ToArray();
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
        var now = DateTime.UtcNow;
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

        entity.LastRunAt = DateTime.UtcNow;
        entity.NextRunAt = nextRunAt.UtcDateTime;
        entity.LastError = Truncate(error, 4000);
        entity.UpdatedAt = DateTime.UtcNow;
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
        entity.LastEvaluatedAt = occurrence.LastEvaluatedAt.UtcDateTime;
        entity.ResolvedAt = ToUtcDateTime(occurrence.ResolvedAt);
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

        var executionCount = await query
            .Select(x => (int?)x.ExecutionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        var lastScheduledUtc = await query
            .Select(x => (DateTime?)x.CreatedAt)
            .MaxAsync(cancellationToken);

        return new ActionScheduleState
        {
            ExecutionCount = executionCount,
            LastScheduledAt = ToDateTimeOffset(lastScheduledUtc)
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
                            x.DueAt <= now.UtcDateTime)
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
            entity.SentAt = DateTime.UtcNow;
        }
        else if (result.IsTransient && nextAttemptAt.HasValue)
        {
            entity.Status = DeliveryStatus.RetryScheduled;
            entity.DueAt = nextAttemptAt.Value.UtcDateTime;
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
        var now = DateTime.UtcNow;
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
        var lastExecutionUtc = await context.Automations
            .AsNoTracking()
            .Select(x => x.LastRunAt)
            .MaxAsync(cancellationToken);

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
            LastExecutionAt = ToDateTimeOffset(lastExecutionUtc)
        };
    }

    private async Task NormalizeStoredDateTimesAsync(
        FlowSentinelDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var closeConnection = connection.State != System.Data.ConnectionState.Open;

        if (closeConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var currentVersion = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken));

            if (currentVersion >= CurrentStorageVersion)
            {
                return;
            }

            var automations = await context.Automations.ToArrayAsync(cancellationToken);
            foreach (var entity in automations)
            {
                entity.LastRunAt = NormalizeUtc(entity.LastRunAt);
                entity.NextRunAt = NormalizeUtc(entity.NextRunAt);
                entity.CreatedAt = NormalizeUtc(entity.CreatedAt);
                entity.UpdatedAt = NormalizeUtc(entity.UpdatedAt);

                context.Entry(entity).Property(x => x.LastRunAt).IsModified = true;
                context.Entry(entity).Property(x => x.NextRunAt).IsModified = true;
                context.Entry(entity).Property(x => x.CreatedAt).IsModified = true;
                context.Entry(entity).Property(x => x.UpdatedAt).IsModified = true;
            }

            var occurrences = await context.Occurrences.ToArrayAsync(cancellationToken);
            foreach (var entity in occurrences)
            {
                entity.OpenedAt = NormalizeUtc(entity.OpenedAt);
                entity.LastEvaluatedAt = NormalizeUtc(entity.LastEvaluatedAt);
                entity.ResolvedAt = NormalizeUtc(entity.ResolvedAt);

                context.Entry(entity).Property(x => x.OpenedAt).IsModified = true;
                context.Entry(entity).Property(x => x.LastEvaluatedAt).IsModified = true;
                context.Entry(entity).Property(x => x.ResolvedAt).IsModified = true;
            }

            var deliveries = await context.Deliveries.ToArrayAsync(cancellationToken);
            foreach (var entity in deliveries)
            {
                entity.CreatedAt = NormalizeUtc(entity.CreatedAt);
                entity.DueAt = NormalizeUtc(entity.DueAt);
                entity.SentAt = NormalizeUtc(entity.SentAt);

                context.Entry(entity).Property(x => x.CreatedAt).IsModified = true;
                context.Entry(entity).Property(x => x.DueAt).IsModified = true;
                context.Entry(entity).Property(x => x.SentAt).IsModified = true;
            }

            var channels = await context.ChannelConfigurations.ToArrayAsync(cancellationToken);
            foreach (var entity in channels)
            {
                entity.CreatedAt = NormalizeUtc(entity.CreatedAt);
                entity.UpdatedAt = NormalizeUtc(entity.UpdatedAt);

                context.Entry(entity).Property(x => x.CreatedAt).IsModified = true;
                context.Entry(entity).Property(x => x.UpdatedAt).IsModified = true;
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            command.CommandText = $"PRAGMA user_version = {CurrentStorageVersion};";
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Persistência SQLite atualizada para a versão {StorageVersion}, com datas normalizadas em UTC.",
                CurrentStorageVersion);
        }
        finally
        {
            if (closeConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task SeedAsync(FlowSentinelDbContext context, CancellationToken cancellationToken)
    {
        if (!await context.ChannelConfigurations.AnyAsync(cancellationToken))
        {
            var now = DateTime.UtcNow;
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
                        var now = DateTime.UtcNow;
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
        OpenedAt = ToDateTimeOffset(entity.OpenedAt),
        LastEvaluatedAt = ToDateTimeOffset(entity.LastEvaluatedAt),
        ResolvedAt = ToDateTimeOffset(entity.ResolvedAt),
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
        OpenedAt = item.OpenedAt.UtcDateTime,
        LastEvaluatedAt = item.LastEvaluatedAt.UtcDateTime,
        ResolvedAt = ToUtcDateTime(item.ResolvedAt),
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
        CreatedAt = ToDateTimeOffset(entity.CreatedAt),
        Status = entity.Status,
        AttemptCount = entity.AttemptCount,
        DueAt = ToDateTimeOffset(entity.DueAt),
        SentAt = ToDateTimeOffset(entity.SentAt),
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
        CreatedAt = item.CreatedAt.UtcDateTime,
        DueAt = item.DueAt.UtcDateTime,
        SentAt = ToUtcDateTime(item.SentAt),
        ExternalMessageId = item.ExternalMessageId,
        LastError = item.LastError,
        FieldsJson = JsonSerializer.Serialize(item.Fields, FlowJson.Options)
    };

    private static AutomationStoreItem Map(AutomationEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Enabled = entity.Enabled,
        IntervalSeconds = entity.IntervalSeconds,
        LastRunAt = ToDateTimeOffset(entity.LastRunAt),
        NextRunAt = ToDateTimeOffset(entity.NextRunAt),
        LastError = entity.LastError
    };

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue
            ? NormalizeUtc(value.Value)
            : null;

    private static DateTime? ToUtcDateTime(DateTimeOffset? value) =>
        value?.UtcDateTime;

    private static DateTimeOffset ToDateTimeOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value.HasValue
            ? ToDateTimeOffset(value.Value)
            : null;

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
