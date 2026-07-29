using System.Text.Json;
using System.Text.Json.Nodes;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Infrastructure.Persistence;

internal sealed class FlowStore : IFlowStore
{
    private const int CurrentStorageVersion = 6;
    private static readonly TimeSpan OccurrenceHeartbeatInterval = TimeSpan.FromMinutes(5);

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
            await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=15000;", cancellationToken);
            await context.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureAdditiveSchemaAsync(context, cancellationToken);
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
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
        var occurrenceIds = await context.Occurrences
            .Where(x => x.AutomationId == automationId)
            .Select(x => x.Id)
            .ToArrayAsync(cancellationToken);
        var deliveries = await context.Deliveries
            .Where(x => x.AutomationId == automationId)
            .ToArrayAsync(cancellationToken);
        var runtimeStates = await context.ActionRuntimeStates
            .Where(x => occurrenceIds.Contains(x.OccurrenceId))
            .ToArrayAsync(cancellationToken);
        var occurrences = await context.Occurrences
            .Where(x => x.AutomationId == automationId)
            .ToArrayAsync(cancellationToken);
        var executionHistory = await context.AutomationExecutionHistory
            .Where(x => x.AutomationId == automationId)
            .ToArrayAsync(cancellationToken);
        var changeHistory = await context.RecordChangeHistory
            .Where(x => x.AutomationId == automationId)
            .ToArrayAsync(cancellationToken);
        context.Deliveries.RemoveRange(deliveries);
        context.ActionRuntimeStates.RemoveRange(runtimeStates);
        context.AutomationExecutionHistory.RemoveRange(executionHistory);
        context.RecordChangeHistory.RemoveRange(changeHistory);
        context.Occurrences.RemoveRange(occurrences);
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

    public async Task AddAutomationExecutionHistoryAsync(
        AutomationExecutionHistoryItem history,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.AutomationExecutionHistory.Add(new AutomationExecutionHistoryEntity
        {
            Id = history.Id,
            AutomationId = history.AutomationId,
            StartedAt = history.StartedAt.UtcDateTime,
            CompletedAt = history.CompletedAt.UtcDateTime,
            Success = history.Success,
            RecordCount = history.RecordCount,
            ChangedRecordCount = history.ChangedRecordCount,
            Error = Truncate(history.Error, 4000)
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddRecordChangeHistoryAsync(
        RecordChangeHistoryItem history,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        context.RecordChangeHistory.Add(new RecordChangeHistoryEntity
        {
            Id = history.Id,
            AutomationId = history.AutomationId,
            OccurrenceId = history.OccurrenceId,
            RecordKey = history.RecordKey,
            DetectedAt = history.DetectedAt.UtcDateTime,
            PreviousSnapshotJson = JsonSerializer.Serialize(history.PreviousSnapshot, FlowJson.Options),
            CurrentSnapshotJson = JsonSerializer.Serialize(history.CurrentSnapshot, FlowJson.Options),
            ChangedFieldsJson = JsonSerializer.Serialize(history.ChangedFields, FlowJson.Options)
        });
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

    public async Task MarkOpenOccurrencesEvaluatedAsync(
        Guid automationId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var evaluatedAtUtc = evaluatedAt.UtcDateTime;
        var staleBeforeUtc = evaluatedAtUtc.Subtract(OccurrenceHeartbeatInterval);
        await context.Occurrences
            .Where(entity =>
                entity.AutomationId == automationId &&
                entity.LastEvaluatedAt < staleBeforeUtc &&
                (entity.Status == OccurrenceStatus.New ||
                 entity.Status == OccurrenceStatus.Active ||
                 entity.Status == OccurrenceStatus.Suspended))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(entity => entity.LastEvaluatedAt, evaluatedAtUtc),
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActionRuntimeStateStoreItem>> GetActionScheduleStatesAsync(
        Guid automationId,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var items = await (
                from state in context.ActionRuntimeStates.AsNoTracking()
                join occurrence in context.Occurrences.AsNoTracking()
                    on state.OccurrenceId equals occurrence.Id
                where occurrence.AutomationId == automationId &&
                      (occurrence.Status == OccurrenceStatus.New ||
                       occurrence.Status == OccurrenceStatus.Active ||
                       occurrence.Status == OccurrenceStatus.Suspended)
                select new
                {
                    state.OccurrenceId,
                    state.ActionId,
                    state.ExecutionCount,
                    state.LastScheduledAt,
                    state.ConditionActive,
                    state.EpisodeNumber
                })
            .ToArrayAsync(cancellationToken);

        return items.Select(item => new ActionRuntimeStateStoreItem
        {
            OccurrenceId = item.OccurrenceId,
            ActionId = item.ActionId,
            State = new ActionScheduleState
            {
                ExecutionCount = item.ExecutionCount,
                LastScheduledAt = ToDateTimeOffset(item.LastScheduledAt),
                ConditionActive = item.ConditionActive,
                EpisodeNumber = item.EpisodeNumber
            }
        }).ToArray();
    }

    public async Task<ActionScheduleState> GetActionScheduleStateAsync(
        Guid occurrenceId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var state = await context.ActionRuntimeStates
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId && x.ActionId == actionId, cancellationToken);
        if (state is not null)
        {
            return Map(state);
        }

        return await BuildLegacyScheduleStateAsync(context, occurrenceId, actionId, cancellationToken);
    }

    public async Task<ActionScheduleState> UpdateActionConditionStateAsync(
        Guid occurrenceId,
        Guid actionId,
        bool conditionActive,
        bool resetOnReentry,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ActionRuntimeStates
            .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId && x.ActionId == actionId, cancellationToken);

        if (entity is null)
        {
            var legacy = await BuildLegacyScheduleStateAsync(context, occurrenceId, actionId, cancellationToken);
            entity = new ActionRuntimeStateEntity
            {
                OccurrenceId = occurrenceId,
                ActionId = actionId,
                ConditionActive = false,
                EpisodeNumber = resetOnReentry ? 0 : legacy.EpisodeNumber,
                ExecutionCount = legacy.ExecutionCount,
                LastScheduledAt = ToUtcDateTime(legacy.LastScheduledAt),
                LastEvaluatedAt = evaluatedAt.UtcDateTime
            };
            context.ActionRuntimeStates.Add(entity);
        }

        if (conditionActive && !entity.ConditionActive && resetOnReentry)
        {
            entity.EpisodeNumber = Math.Max(0, entity.EpisodeNumber) + 1;
            entity.ExecutionCount = 0;
            entity.LastScheduledAt = null;
        }
        else if (conditionActive && entity.EpisodeNumber <= 0 && resetOnReentry)
        {
            entity.EpisodeNumber = 1;
        }

        entity.ConditionActive = conditionActive;
        entity.LastEvaluatedAt = evaluatedAt.UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task MarkActionScheduledAsync(
        Guid occurrenceId,
        Guid actionId,
        int episodeNumber,
        int executionNumber,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ActionRuntimeStates
            .SingleOrDefaultAsync(x => x.OccurrenceId == occurrenceId && x.ActionId == actionId, cancellationToken);
        if (entity is null)
        {
            entity = new ActionRuntimeStateEntity
            {
                OccurrenceId = occurrenceId,
                ActionId = actionId,
                ConditionActive = true,
                EpisodeNumber = Math.Max(0, episodeNumber),
                LastEvaluatedAt = scheduledAt.UtcDateTime
            };
            context.ActionRuntimeStates.Add(entity);
        }

        entity.ExecutionCount = Math.Max(entity.ExecutionCount, executionNumber);
        entity.LastScheduledAt = scheduledAt.UtcDateTime;
        entity.LastEvaluatedAt = scheduledAt.UtcDateTime;
        await context.SaveChangesAsync(cancellationToken);
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

    public Task CancelPendingDeliveriesAsync(Guid occurrenceId, CancellationToken cancellationToken) =>
        CancelPendingDeliveriesInternalAsync(occurrenceId, actionId: null, cancellationToken);

    public Task CancelPendingDeliveriesAsync(Guid occurrenceId, Guid actionId, CancellationToken cancellationToken) =>
        CancelPendingDeliveriesInternalAsync(occurrenceId, actionId, cancellationToken);

    private async Task CancelPendingDeliveriesInternalAsync(
        Guid occurrenceId,
        Guid? actionId,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var context = await _factory.CreateDbContextAsync(cancellationToken);
        var query = context.Deliveries.Where(x =>
            x.OccurrenceId == occurrenceId &&
            (x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.RetryScheduled));
        if (actionId.HasValue)
        {
            query = query.Where(x => x.ActionId == actionId.Value);
        }

        var deliveries = await query.ToArrayAsync(cancellationToken);
        foreach (var delivery in deliveries)
        {
            delivery.Status = DeliveryStatus.Cancelled;
            delivery.LastError = "Entrega cancelada porque a condição de monitoramento deixou de estar ativa.";
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

        if (result.IsSkipped)
        {
            entity.Status = DeliveryStatus.Skipped;
            entity.SentAt = null;
        }
        else if (result.Success)
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

    private static async Task EnsureAdditiveSchemaAsync(
        FlowSentinelDbContext context,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS action_runtime_states (
                OccurrenceId TEXT NOT NULL,
                ActionId TEXT NOT NULL,
                ConditionActive INTEGER NOT NULL DEFAULT 0,
                EpisodeNumber INTEGER NOT NULL DEFAULT 0,
                ExecutionCount INTEGER NOT NULL DEFAULT 0,
                LastScheduledAt TEXT NULL,
                LastEvaluatedAt TEXT NOT NULL,
                CONSTRAINT PK_action_runtime_states PRIMARY KEY (OccurrenceId, ActionId)
            );
            """,
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_action_runtime_states_ConditionActive_LastEvaluatedAt ON action_runtime_states (ConditionActive, LastEvaluatedAt);",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS automation_execution_history (
                Id TEXT NOT NULL CONSTRAINT PK_automation_execution_history PRIMARY KEY,
                AutomationId TEXT NOT NULL,
                StartedAt TEXT NOT NULL,
                CompletedAt TEXT NOT NULL,
                Success INTEGER NOT NULL,
                RecordCount INTEGER NOT NULL,
                ChangedRecordCount INTEGER NOT NULL,
                Error TEXT NULL
            );
            """,
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_automation_execution_history_AutomationId_StartedAt ON automation_execution_history (AutomationId, StartedAt);",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS record_change_history (
                Id TEXT NOT NULL CONSTRAINT PK_record_change_history PRIMARY KEY,
                AutomationId TEXT NOT NULL,
                OccurrenceId TEXT NOT NULL,
                RecordKey TEXT NOT NULL,
                DetectedAt TEXT NOT NULL,
                PreviousSnapshotJson TEXT NOT NULL,
                CurrentSnapshotJson TEXT NOT NULL,
                ChangedFieldsJson TEXT NOT NULL
            );
            """,
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_record_change_history_AutomationId_DetectedAt ON record_change_history (AutomationId, DetectedAt);",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_record_change_history_OccurrenceId_DetectedAt ON record_change_history (OccurrenceId, DetectedAt);",
            cancellationToken);
    }

    private static async Task<ActionScheduleState> BuildLegacyScheduleStateAsync(
        FlowSentinelDbContext context,
        Guid occurrenceId,
        Guid actionId,
        CancellationToken cancellationToken)
    {
        var query = context.Deliveries.AsNoTracking()
            .Where(x => x.OccurrenceId == occurrenceId && x.ActionId == actionId);
        var executionCount = await query.Select(x => (int?)x.ExecutionNumber).MaxAsync(cancellationToken) ?? 0;
        var lastScheduledUtc = await query.Select(x => (DateTime?)x.CreatedAt).MaxAsync(cancellationToken);
        return new ActionScheduleState
        {
            ExecutionCount = executionCount,
            LastScheduledAt = ToDateTimeOffset(lastScheduledUtc),
            ConditionActive = false,
            EpisodeNumber = 0
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

            // Bancos antigos podiam conter datas com deslocamento explícito. A normalização
            // é feita em SQL e somente nas linhas que realmente possuem sufixo de fuso,
            // evitando carregar e regravar milhares de ocorrências e entregas no startup.
            await NormalizeLegacyDateTimeColumnsAsync(context, cancellationToken);

            var disabledLegacyAggregateActionIds = new HashSet<Guid>();
            var automations = await context.Automations.ToArrayAsync(cancellationToken);
            foreach (var entity in automations)
            {
                UpgradeLegacyWorkbookDefinition(entity, disabledLegacyAggregateActionIds);
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            // A versão 0.4.1/0.4.2 criava um estado inativo para cada ação e registro,
            // mesmo quando nenhuma condição havia sido atendida. Esses estados vazios não
            // carregam histórico útil e podem ser recriados sob demanda. A limpeza evita
            // carregar milhares de linhas redundantes a cada ciclo do agendador.
            await context.ActionRuntimeStates
                .Where(entity =>
                    !entity.ConditionActive &&
                    entity.ExecutionCount == 0 &&
                    entity.EpisodeNumber == 0 &&
                    entity.LastScheduledAt == null)
                .ExecuteDeleteAsync(cancellationToken);

            if (disabledLegacyAggregateActionIds.Count > 0)
            {
                await context.Deliveries
                    .Where(entity =>
                        disabledLegacyAggregateActionIds.Contains(entity.ActionId) &&
                        (entity.Status == DeliveryStatus.Pending ||
                         entity.Status == DeliveryStatus.RetryScheduled ||
                         entity.Status == DeliveryStatus.Processing))
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(entity => entity.Status, DeliveryStatus.Cancelled)
                            .SetProperty(
                                entity => entity.LastError,
                                "Entrega cancelada pela atualização do assistente de planilhas: indicadores legados foram desativados."),
                        cancellationToken);
            }

            var enabledChannelIds = await context.ChannelConfigurations
                .AsNoTracking()
                .Where(entity => entity.Enabled)
                .Select(entity => entity.Id)
                .ToArrayAsync(cancellationToken);

            await context.Deliveries
                .Where(entity =>
                    !enabledChannelIds.Contains(entity.ChannelConfigurationId) &&
                    (entity.Status == DeliveryStatus.Pending ||
                     entity.Status == DeliveryStatus.RetryScheduled ||
                     entity.Status == DeliveryStatus.Processing ||
                     entity.Status == DeliveryStatus.Failed))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(entity => entity.Status, DeliveryStatus.Skipped)
                        .SetProperty(
                            entity => entity.LastError,
                            "Canal removido ou desabilitado; entrega ignorada pela atualização sem registrar falha operacional."),
                    cancellationToken);

            command.CommandText = $"PRAGMA user_version = {CurrentStorageVersion};";
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation(
                "Persistência SQLite atualizada para a versão {StorageVersion} por migração incremental otimizada.",
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

    private static async Task NormalizeLegacyDateTimeColumnsAsync(
        FlowSentinelDbContext context,
        CancellationToken cancellationToken)
    {
        (string Table, string Column)[] columns =
        [
            ("automations", "LastRunAt"),
            ("automations", "NextRunAt"),
            ("automations", "CreatedAt"),
            ("automations", "UpdatedAt"),
            ("occurrences", "OpenedAt"),
            ("occurrences", "LastEvaluatedAt"),
            ("occurrences", "ResolvedAt"),
            ("deliveries", "CreatedAt"),
            ("deliveries", "DueAt"),
            ("deliveries", "SentAt"),
            ("channel_configurations", "CreatedAt"),
            ("channel_configurations", "UpdatedAt"),
            ("action_runtime_states", "LastScheduledAt"),
            ("action_runtime_states", "LastEvaluatedAt"),
            ("automation_execution_history", "StartedAt"),
            ("automation_execution_history", "CompletedAt"),
            ("record_change_history", "DetectedAt")
        ];

        foreach (var (table, column) in columns)
        {
            await context.Database.ExecuteSqlRawAsync(
                $"""
                UPDATE "{table}"
                SET "{column}" = strftime('%Y-%m-%d %H:%M:%f', "{column}")
                WHERE "{column}" IS NOT NULL
                  AND (
                        (length("{column}") >= 6
                         AND substr("{column}", -6, 1) IN ('+', '-')
                         AND substr("{column}", -3, 1) = ':')
                        OR substr("{column}", -1, 1) IN ('Z', 'z')
                      );
                """,
                cancellationToken);
        }
    }

    private void UpgradeLegacyWorkbookDefinition(
        AutomationEntity entity,
        ISet<Guid> disabledAggregateActionIds)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<AutomationDefinition>(entity.DefinitionJson, FlowJson.Options);
            if (definition is null)
            {
                return;
            }

            var changed = false;
            foreach (var action in definition.Actions)
            {
                foreach (var channel in action.Channels.Where(x => x.ChannelType == ChannelType.LocalWindows))
                {
                    if (channel.GroupingMode != NotificationGroupingMode.Individual || channel.GroupingWindowSeconds != 0)
                    {
                        channel.GroupingMode = NotificationGroupingMode.Individual;
                        channel.GroupingWindowSeconds = 0;
                        changed = true;
                    }
                }
            }

            var source = definition.Sources.FirstOrDefault(x => x.Type == SourceType.Excel &&
                                                                 x.Configuration.ValueKind == JsonValueKind.Object);
            var profileName = source is not null && source.Configuration.TryGetProperty("profileName", out var profileElement)
                ? profileElement.GetString() ?? string.Empty
                : string.Empty;
            var mode = source is not null && source.Configuration.TryGetProperty("mode", out var modeElement)
                ? modeElement.GetString() ?? string.Empty
                : string.Empty;
            var sourceName = source?.Name ?? string.Empty;
            var isRp102 = source is not null &&
                          string.Equals(mode, "SectionedMatrix", StringComparison.OrdinalIgnoreCase) &&
                          (profileName.Contains("RP-102", StringComparison.OrdinalIgnoreCase) ||
                           sourceName.Contains("RP-102", StringComparison.OrdinalIgnoreCase) ||
                           definition.Name.Contains("RP-102", StringComparison.OrdinalIgnoreCase));

            if (isRp102)
            {
                foreach (var action in definition.Actions.Where(x =>
                             string.Equals(x.Name, "Mudança de quantidade por situação", StringComparison.OrdinalIgnoreCase) ||
                             x.MessageTemplate.Contains("O indicador {{Metric}}", StringComparison.OrdinalIgnoreCase)))
                {
                    if (action.Enabled)
                    {
                        action.Enabled = false;
                        changed = true;
                    }
                    disabledAggregateActionIds.Add(action.Id);
                }

                var root = JsonNode.Parse(source!.Configuration.GetRawText()) as JsonObject;
                var matrix = root?["matrix"] as JsonObject;
                if (matrix is not null)
                {
                    changed |= SetBoolean(matrix, "generateAggregateRecords", false);
                    changed |= SetBoolean(matrix, "aggregateGlobal", false);
                    changed |= SetBoolean(matrix, "aggregateBySection", false);
                    changed |= SetBoolean(matrix, "aggregateByCollaborator", false);
                    changed |= SetBoolean(matrix, "includeBlankValuesInAggregates", false);
                    using var upgradedDocument = JsonDocument.Parse(root!.ToJsonString(FlowJson.Options));
                    source.Configuration = upgradedDocument.RootElement.Clone();
                }
            }

            if (!changed)
            {
                return;
            }

            entity.DefinitionJson = JsonSerializer.Serialize(definition, FlowJson.Options);
            entity.UpdatedAt = DateTime.UtcNow;
            if (isRp102)
            {
                _logger.LogInformation(
                    "Automação legada RP-102 '{AutomationName}' atualizada: indicadores agregados automáticos foram desativados para evitar notificações derivadas em excesso.",
                    definition.Name);
            }
            else
            {
                _logger.LogInformation(
                    "Automação '{AutomationName}' atualizada para manter notificações locais do Windows no modo individual.",
                    definition.Name);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Não foi possível atualizar a automação legada '{AutomationName}'.", entity.Name);
        }
    }

    private static bool SetBoolean(JsonObject target, string propertyName, bool value)
    {
        if (target[propertyName] is JsonValue currentNode &&
            currentNode.TryGetValue<bool>(out var current) &&
            current == value)
        {
            return false;
        }
        target[propertyName] = value;
        return true;
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

    private static ActionScheduleState Map(ActionRuntimeStateEntity entity) => new()
    {
        ExecutionCount = entity.ExecutionCount,
        LastScheduledAt = ToDateTimeOffset(entity.LastScheduledAt),
        ConditionActive = entity.ConditionActive,
        EpisodeNumber = entity.EpisodeNumber
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
