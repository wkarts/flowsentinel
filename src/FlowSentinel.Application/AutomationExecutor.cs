using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FlowSentinel.Domain;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Application;

public sealed class AutomationExecutor : IAutomationExecutor, IAutomationControl
{
    private readonly IFlowStore _store;
    private readonly IReadOnlyDictionary<SourceType, IDataSourceReader> _readers;
    private readonly IRuleEngine _ruleEngine;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IRecipientResolver _recipientResolver;
    private readonly ILogger<AutomationExecutor> _logger;
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    public AutomationExecutor(
        IFlowStore store,
        IEnumerable<IDataSourceReader> readers,
        IRuleEngine ruleEngine,
        ITemplateRenderer templateRenderer,
        IRecipientResolver recipientResolver,
        ILogger<AutomationExecutor> logger)
    {
        _store = store;
        _readers = readers.ToDictionary(x => x.SourceType);
        _ruleEngine = ruleEngine;
        _templateRenderer = templateRenderer;
        _recipientResolver = recipientResolver;
        _logger = logger;
    }

    public Task ExecuteNowAsync(Guid automationId, CancellationToken cancellationToken) =>
        ExecuteAsync(automationId, cancellationToken);

    public async Task ExecuteAsync(Guid automationId, CancellationToken cancellationToken)
    {
        var semaphore = _locks.GetOrAdd(automationId, _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("Automação {AutomationId} já está em execução.", automationId);
            return;
        }

        var startedAt = DateTimeOffset.Now;
        var recordCount = 0;
        var changedRecordCount = 0;

        try
        {
            var automation = await _store.GetAutomationDefinitionAsync(automationId, cancellationToken);
            if (automation is null || !automation.Enabled)
            {
                return;
            }

            automation.Validate();
            var now = DateTimeOffset.Now;
            var channelConfigurations = (await _store.GetChannelConfigurationsAsync(cancellationToken))
                .ToDictionary(x => x.Id);
            var openOccurrences = (await _store.GetOpenOccurrencesAsync(automation.Id, cancellationToken))
                .GroupBy(item => item.RecordKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.OpenedAt).First(), StringComparer.OrdinalIgnoreCase);
            var actionStates = (await _store.GetActionScheduleStatesAsync(automation.Id, cancellationToken))
                .ToDictionary(item => (item.OccurrenceId, item.ActionId), item => item.State);

            var sourceResults = await ReadSourcesAsync(automation, cancellationToken);
            var mergedRecords = MergeRecords(automation, sourceResults);
            recordCount = mergedRecords.Count;
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in mergedRecords)
            {
                cancellationToken.ThrowIfCancellationRequested();
                seenKeys.Add(record.Key);
                openOccurrences.TryGetValue(record.Key, out var occurrence);
                if (await EvaluateRecordAsync(
                        automation,
                        record,
                        occurrence,
                        openOccurrences,
                        channelConfigurations,
                        actionStates,
                        now,
                        cancellationToken))
                {
                    changedRecordCount++;
                }
            }

            await _store.MarkOpenOccurrencesEvaluatedAsync(automation.Id, now, cancellationToken);
            await ResolveMissingRecordsAsync(automation, openOccurrences.Values, seenKeys, now, cancellationToken);
            await _store.MarkAutomationExecutionAsync(
                automation.Id,
                now.AddSeconds(automation.IntervalSeconds),
                null,
                cancellationToken);
            await AddExecutionHistorySafelyAsync(
                automation.Id, startedAt, DateTimeOffset.Now, true, recordCount, changedRecordCount, null, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha ao executar automação {AutomationId}.", automationId);
            var definition = await _store.GetAutomationDefinitionAsync(automationId, cancellationToken);
            var interval = definition?.IntervalSeconds ?? 300;
            await _store.MarkAutomationExecutionAsync(
                automationId,
                DateTimeOffset.Now.AddSeconds(Math.Max(30, interval)),
                exception.Message,
                cancellationToken);
            await AddExecutionHistorySafelyAsync(
                automationId, startedAt, DateTimeOffset.Now, false, recordCount, changedRecordCount, exception.Message, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<IReadOnlyCollection<SourceReadResult>> ReadSourcesAsync(
        AutomationDefinition automation,
        CancellationToken cancellationToken)
    {
        var tasks = automation.Sources
            .Where(x => x.Enabled)
            .Select(async source =>
            {
                if (!_readers.TryGetValue(source.Type, out var reader))
                {
                    throw new InvalidOperationException($"Não existe leitor registrado para {source.Type}.");
                }

                return await reader.ReadAsync(source, cancellationToken);
            });

        return await Task.WhenAll(tasks);
    }

    private static IReadOnlyCollection<MergedRecord> MergeRecords(
        AutomationDefinition automation,
        IReadOnlyCollection<SourceReadResult> results)
    {
        var primarySource = automation.Sources.Single(x => x.IsPrimary);
        var primary = results.Single(x => string.Equals(x.Alias, primarySource.Alias, StringComparison.OrdinalIgnoreCase));
        var secondaryIndexes = results
            .Where(x => !string.Equals(x.Alias, primarySource.Alias, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                x => x.Alias,
                x => x.Records.GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        var merged = new List<MergedRecord>();
        foreach (var primaryRecord in primary.Records)
        {
            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            AddFields(fields, primaryRecord, includeUnqualified: true);

            foreach (var index in secondaryIndexes)
            {
                if (index.Value.TryGetValue(primaryRecord.Key, out var secondaryRecord))
                {
                    AddFields(fields, secondaryRecord, includeUnqualified: false);
                }
            }

            merged.Add(new MergedRecord(
                primaryRecord.Key,
                fields,
                ComputeFingerprint(fields)));
        }

        return merged;
    }

    private static void AddFields(
        IDictionary<string, string?> destination,
        DataRecord record,
        bool includeUnqualified)
    {
        foreach (var field in record.Fields)
        {
            destination[$"{record.SourceAlias}.{field.Key}"] = field.Value;
            if (includeUnqualified)
            {
                destination[field.Key] = field.Value;
            }
        }
    }

    private async Task<bool> EvaluateRecordAsync(
        AutomationDefinition automation,
        MergedRecord record,
        OccurrenceStoreItem? occurrence,
        IDictionary<string, OccurrenceStoreItem> openOccurrences,
        IReadOnlyDictionary<Guid, ChannelConfiguration> channelConfigurations,
        IDictionary<(Guid OccurrenceId, Guid ActionId), ActionScheduleState> actionStates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previous = occurrence?.Snapshot ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var context = new EvaluationContext
        {
            Automation = automation,
            RecordKey = record.Key,
            Fields = record.Fields,
            PreviousFields = previous,
            Now = now
        };

        if (occurrence is null)
        {
            if (!_ruleEngine.Evaluate(automation.EntryRules, context))
            {
                return false;
            }

            occurrence = new OccurrenceStoreItem
            {
                Id = Guid.NewGuid(),
                AutomationId = automation.Id,
                RecordKey = record.Key,
                Status = OccurrenceStatus.New,
                OpenedAt = now,
                LastEvaluatedAt = now,
                Snapshot = record.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                Fingerprint = record.Fingerprint
            };
            await _store.CreateOccurrenceAsync(occurrence, cancellationToken);
            openOccurrences[record.Key] = occurrence;
            await ScheduleActionsAsync(
                automation, occurrence, context, channelConfigurations, actionStates, ActionTrigger.OnOpen, now, cancellationToken);
            await ScheduleActionsAsync(
                automation, occurrence, context, channelConfigurations, actionStates, ActionTrigger.WhileActive, now, cancellationToken,
                openingOccurrence: true);
            occurrence.Status = OccurrenceStatus.Active;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            return false;
        }

        var previousStatus = occurrence.Status;
        var recordChanged = !string.Equals(occurrence.Fingerprint, record.Fingerprint, StringComparison.Ordinal);
        if (recordChanged)
        {
            await AddRecordChangeHistorySafelyAsync(new RecordChangeHistoryItem
            {
                AutomationId = automation.Id,
                OccurrenceId = occurrence.Id,
                RecordKey = record.Key,
                DetectedAt = now,
                PreviousSnapshot = previous.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                CurrentSnapshot = record.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                ChangedFields = GetChangedFields(previous, record.Fields)
            }, cancellationToken);

            occurrence.Snapshot = record.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            occurrence.Fingerprint = record.Fingerprint;
        }
        occurrence.LastEvaluatedAt = now;

        if (_ruleEngine.Evaluate(automation.CompletionRules, context, defaultValue: false))
        {
            occurrence.Status = OccurrenceStatus.Resolved;
            occurrence.ResolvedAt = now;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            await _store.CancelPendingDeliveriesAsync(occurrence.Id, cancellationToken);
            await ScheduleActionsAsync(
                automation, occurrence, context, channelConfigurations, actionStates, ActionTrigger.OnResolved, now, cancellationToken);
            return recordChanged;
        }

        if (_ruleEngine.Evaluate(automation.SuspensionRules, context, defaultValue: false))
        {
            occurrence.Status = OccurrenceStatus.Suspended;
            if (recordChanged || previousStatus != OccurrenceStatus.Suspended)
            {
                await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            }
            return recordChanged;
        }

        if (automation.ResolveWhenPersistenceFails &&
            automation.PersistenceRules is not null &&
            !_ruleEngine.Evaluate(automation.PersistenceRules, context))
        {
            occurrence.Status = OccurrenceStatus.Resolved;
            occurrence.ResolvedAt = now;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            await _store.CancelPendingDeliveriesAsync(occurrence.Id, cancellationToken);
            await ScheduleActionsAsync(
                automation, occurrence, context, channelConfigurations, actionStates, ActionTrigger.OnResolved, now, cancellationToken);
            return recordChanged;
        }

        occurrence.Status = OccurrenceStatus.Active;
        if (recordChanged || previousStatus != OccurrenceStatus.Active)
        {
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
        }
        await ScheduleActionsAsync(
            automation, occurrence, context, channelConfigurations, actionStates, ActionTrigger.WhileActive, now, cancellationToken);
        return recordChanged;
    }

    private async Task ScheduleActionsAsync(
        AutomationDefinition automation,
        OccurrenceStoreItem occurrence,
        EvaluationContext context,
        IReadOnlyDictionary<Guid, ChannelConfiguration> channelConfigurations,
        IDictionary<(Guid OccurrenceId, Guid ActionId), ActionScheduleState> actionStates,
        ActionTrigger trigger,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool openingOccurrence = false)
    {
        foreach (var action in automation.Actions.Where(x => x.Enabled && x.Trigger == trigger))
        {
            if (openingOccurrence && trigger == ActionTrigger.WhileActive && !action.EvaluateWhileActiveOnOpen)
            {
                continue;
            }

            ActionScheduleState state;
            if (trigger == ActionTrigger.WhileActive)
            {
                state = await EvaluateWhileActiveStateAsync(
                    action, occurrence, context, actionStates, now, cancellationToken);
                if (!state.ConditionActive)
                {
                    if (action.CancelPendingWhenConditionFails)
                    {
                        await _store.CancelPendingDeliveriesAsync(occurrence.Id, action.Id, cancellationToken);
                    }
                    continue;
                }
            }
            else
            {
                if (!_ruleEngine.Evaluate(action.Conditions, context))
                {
                    continue;
                }
                state = await GetScheduleStateAsync(occurrence.Id, action.Id, actionStates, cancellationToken);
            }

            if (!(action.Schedule ?? new ActionScheduleDefinition()).IsAllowed(now))
            {
                continue;
            }

            if (!action.Repeat.AllowsExecution(state.ExecutionCount))
            {
                continue;
            }

            if (state.ExecutionCount > 0 && state.LastScheduledAt.HasValue)
            {
                var nextAllowed = state.LastScheduledAt.Value.AddSeconds(action.Repeat.IntervalSeconds);
                if (nextAllowed > now)
                {
                    continue;
                }
            }

            var executionNumber = state.ExecutionCount + 1;
            var subject = _templateRenderer.Render(action.SubjectTemplate, context);
            var message = _templateRenderer.Render(action.MessageTemplate, context);
            var deliveries = new List<DeliveryStoreItem>();

            foreach (var channel in action.Channels.OrderBy(x => x.Order))
            {
                if (!channelConfigurations.TryGetValue(channel.ChannelConfigurationId, out var configuration) ||
                    !configuration.Enabled ||
                    configuration.Type != channel.ChannelType)
                {
                    _logger.LogDebug(
                        "Canal {ChannelConfigurationId} ignorado ao agendar a ação {ActionName}: configuração ausente, desabilitada ou incompatível.",
                        channel.ChannelConfigurationId,
                        action.Name);
                    continue;
                }

                var groupingDelay = channel.ChannelType == ChannelType.LocalWindows ||
                                    channel.GroupingMode == NotificationGroupingMode.Individual
                    ? 0
                    : Math.Max(0, channel.GroupingWindowSeconds);
                var dueAt = now.AddSeconds(Math.Max(Math.Max(0, action.DelaySeconds), groupingDelay));
                var recipients = await _recipientResolver.ResolveAsync(automation, action, channel.ChannelType, context, cancellationToken);
                foreach (var recipient in recipients)
                {
                    var key = BuildIdempotencyKey(
                        occurrence.Id,
                        action.Id,
                        channel.ChannelConfigurationId,
                        recipient.Address,
                        state.EpisodeNumber,
                        executionNumber);

                    deliveries.Add(new DeliveryStoreItem
                    {
                        Id = Guid.NewGuid(),
                        OccurrenceId = occurrence.Id,
                        AutomationId = automation.Id,
                        ActionId = action.Id,
                        ChannelConfigurationId = channel.ChannelConfigurationId,
                        ChannelType = channel.ChannelType,
                        Recipient = recipient.Address,
                        Subject = subject,
                        Message = message,
                        IdempotencyKey = key,
                        ExecutionNumber = executionNumber,
                        CreatedAt = now,
                        Status = DeliveryStatus.Pending,
                        AttemptCount = 0,
                        DueAt = dueAt,
                        Fields = context.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                    });
                }
            }

            if (deliveries.Count == 0)
            {
                continue;
            }

            await _store.AddDeliveriesAsync(deliveries, cancellationToken);
            await _store.MarkActionScheduledAsync(
                occurrence.Id,
                action.Id,
                state.EpisodeNumber,
                executionNumber,
                now,
                cancellationToken);
            actionStates[(occurrence.Id, action.Id)] = new ActionScheduleState
            {
                ConditionActive = state.ConditionActive,
                EpisodeNumber = state.EpisodeNumber,
                ExecutionCount = executionNumber,
                LastScheduledAt = now
            };
        }
    }

    private async Task<ActionScheduleState> EvaluateWhileActiveStateAsync(
        ActionDefinition action,
        OccurrenceStoreItem occurrence,
        EvaluationContext context,
        IDictionary<(Guid OccurrenceId, Guid ActionId), ActionScheduleState> actionStates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var key = (occurrence.Id, action.Id);
        var activationMatches = _ruleEngine.Evaluate(action.Conditions, context);
        var hasPersistentLifecycle = action.PersistenceConditions is not null ||
                                     action.CompletionConditions is not null ||
                                     action.Repeat.ResetOnConditionReentry;

        if (!hasPersistentLifecycle)
        {
            if (!activationMatches)
            {
                return new ActionScheduleState { ConditionActive = false };
            }

            if (actionStates.TryGetValue(key, out var cached))
            {
                return cached.ConditionActive
                    ? cached
                    : await UpdateActionConditionStateAsync(
                        occurrence.Id, action, true, actionStates, now, cancellationToken);
            }

            return await UpdateActionConditionStateAsync(
                occurrence.Id, action, true, actionStates, now, cancellationToken);
        }

        if (!actionStates.TryGetValue(key, out var current))
        {
            if (!activationMatches)
            {
                return new ActionScheduleState { ConditionActive = false };
            }

            return await UpdateActionConditionStateAsync(
                occurrence.Id, action, true, actionStates, now, cancellationToken);
        }

        bool conditionActive;
        if (!current.ConditionActive)
        {
            conditionActive = activationMatches;
        }
        else if (_ruleEngine.Evaluate(action.CompletionConditions, context, defaultValue: false))
        {
            conditionActive = false;
        }
        else if (action.PersistenceConditions is not null)
        {
            conditionActive = _ruleEngine.Evaluate(action.PersistenceConditions, context);
        }
        else
        {
            conditionActive = activationMatches;
        }

        if (conditionActive == current.ConditionActive)
        {
            return current;
        }

        return await UpdateActionConditionStateAsync(
            occurrence.Id, action, conditionActive, actionStates, now, cancellationToken);
    }

    private async Task<ActionScheduleState> UpdateActionConditionStateAsync(
        Guid occurrenceId,
        ActionDefinition action,
        bool conditionActive,
        IDictionary<(Guid OccurrenceId, Guid ActionId), ActionScheduleState> actionStates,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updated = await _store.UpdateActionConditionStateAsync(
            occurrenceId,
            action.Id,
            conditionActive,
            action.Repeat.ResetOnConditionReentry,
            now,
            cancellationToken);
        actionStates[(occurrenceId, action.Id)] = updated;
        return updated;
    }

    private async Task<ActionScheduleState> GetScheduleStateAsync(
        Guid occurrenceId,
        Guid actionId,
        IDictionary<(Guid OccurrenceId, Guid ActionId), ActionScheduleState> actionStates,
        CancellationToken cancellationToken)
    {
        var key = (occurrenceId, actionId);
        if (actionStates.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await _store.GetActionScheduleStateAsync(occurrenceId, actionId, cancellationToken);
        actionStates[key] = loaded;
        return loaded;
    }

    private async Task AddExecutionHistorySafelyAsync(
        Guid automationId,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        bool success,
        int recordCount,
        int changedRecordCount,
        string? error,
        CancellationToken cancellationToken)
    {
        try
        {
            await _store.AddAutomationExecutionHistoryAsync(new AutomationExecutionHistoryItem
            {
                AutomationId = automationId,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Success = success,
                RecordCount = recordCount,
                ChangedRecordCount = changedRecordCount,
                Error = error
            }, cancellationToken);
        }
        catch (Exception historyException)
        {
            _logger.LogWarning(historyException,
                "Não foi possível registrar o histórico da execução da automação {AutomationId}.", automationId);
        }
    }

    private async Task AddRecordChangeHistorySafelyAsync(
        RecordChangeHistoryItem history,
        CancellationToken cancellationToken)
    {
        try
        {
            await _store.AddRecordChangeHistoryAsync(history, cancellationToken);
        }
        catch (Exception historyException)
        {
            _logger.LogWarning(historyException,
                "Não foi possível registrar o histórico da mudança do registro {RecordKey} da automação {AutomationId}.",
                history.RecordKey,
                history.AutomationId);
        }
    }

    private static List<string> GetChangedFields(
        IReadOnlyDictionary<string, string?> previous,
        IReadOnlyDictionary<string, string?> current)
    {
        return previous.Keys
            .Union(current.Keys, StringComparer.OrdinalIgnoreCase)
            .Where(field => !string.Equals(
                previous.GetValueOrDefault(field),
                current.GetValueOrDefault(field),
                StringComparison.Ordinal))
            .OrderBy(field => field, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ResolveMissingRecordsAsync(
        AutomationDefinition automation,
        IReadOnlyCollection<OccurrenceStoreItem> openOccurrences,
        ISet<string> seenKeys,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (automation.MissingRecordBehavior != MissingRecordBehavior.Resolve)
        {
            return;
        }

        foreach (var occurrence in openOccurrences.Where(x => !seenKeys.Contains(x.RecordKey)))
        {
            occurrence.Status = OccurrenceStatus.Resolved;
            occurrence.ResolvedAt = now;
            occurrence.LastEvaluatedAt = now;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            await _store.CancelPendingDeliveriesAsync(occurrence.Id, cancellationToken);
        }
    }

    private static string ComputeFingerprint(IReadOnlyDictionary<string, string?> fields)
    {
        var normalized = string.Join("\n", fields.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Key}={x.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string BuildIdempotencyKey(
        Guid occurrenceId,
        Guid actionId,
        Guid channelId,
        string recipient,
        int episodeNumber,
        int execution)
    {
        var normalizedRecipient = recipient.Trim().ToLowerInvariant();
        var source = episodeNumber > 0
            ? $"{occurrenceId:N}|{actionId:N}|{channelId:N}|{normalizedRecipient}|episode:{episodeNumber}|{execution}"
            : $"{occurrenceId:N}|{actionId:N}|{channelId:N}|{normalizedRecipient}|{execution}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private sealed record MergedRecord(
        string Key,
        IReadOnlyDictionary<string, string?> Fields,
        string Fingerprint);
}
