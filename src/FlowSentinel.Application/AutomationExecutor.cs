using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            var sourceResults = await ReadSourcesAsync(automation, cancellationToken);
            var mergedRecords = MergeRecords(automation, sourceResults);
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var record in mergedRecords)
            {
                cancellationToken.ThrowIfCancellationRequested();
                seenKeys.Add(record.Key);
                await EvaluateRecordAsync(automation, record, channelConfigurations, now, cancellationToken);
            }

            await ResolveMissingRecordsAsync(automation, seenKeys, now, cancellationToken);
            await _store.MarkAutomationExecutionAsync(
                automation.Id,
                now.AddSeconds(automation.IntervalSeconds),
                null,
                cancellationToken);
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

    private async Task EvaluateRecordAsync(
        AutomationDefinition automation,
        MergedRecord record,
        IReadOnlyDictionary<Guid, ChannelConfiguration> channelConfigurations,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var occurrence = await _store.GetOpenOccurrenceAsync(automation.Id, record.Key, cancellationToken);
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
                return;
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
            await ScheduleActionsAsync(automation, occurrence, context, channelConfigurations, ActionTrigger.OnOpen, now, cancellationToken);
            occurrence.Status = OccurrenceStatus.Active;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            return;
        }

        occurrence.LastEvaluatedAt = now;
        occurrence.Snapshot = record.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        occurrence.Fingerprint = record.Fingerprint;

        if (_ruleEngine.Evaluate(automation.CompletionRules, context, defaultValue: false))
        {
            occurrence.Status = OccurrenceStatus.Resolved;
            occurrence.ResolvedAt = now;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            await _store.CancelPendingDeliveriesAsync(occurrence.Id, cancellationToken);
            await ScheduleActionsAsync(automation, occurrence, context, channelConfigurations, ActionTrigger.OnResolved, now, cancellationToken);
            return;
        }

        if (_ruleEngine.Evaluate(automation.SuspensionRules, context, defaultValue: false))
        {
            occurrence.Status = OccurrenceStatus.Suspended;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            return;
        }

        if (automation.ResolveWhenPersistenceFails &&
            automation.PersistenceRules is not null &&
            !_ruleEngine.Evaluate(automation.PersistenceRules, context))
        {
            occurrence.Status = OccurrenceStatus.Resolved;
            occurrence.ResolvedAt = now;
            await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
            await _store.CancelPendingDeliveriesAsync(occurrence.Id, cancellationToken);
            await ScheduleActionsAsync(automation, occurrence, context, channelConfigurations, ActionTrigger.OnResolved, now, cancellationToken);
            return;
        }

        occurrence.Status = OccurrenceStatus.Active;
        await _store.UpdateOccurrenceAsync(occurrence, cancellationToken);
        await ScheduleActionsAsync(automation, occurrence, context, channelConfigurations, ActionTrigger.WhileActive, now, cancellationToken);
    }

    private async Task ScheduleActionsAsync(
        AutomationDefinition automation,
        OccurrenceStoreItem occurrence,
        EvaluationContext context,
        IReadOnlyDictionary<Guid, ChannelConfiguration> channelConfigurations,
        ActionTrigger trigger,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var action in automation.Actions.Where(x => x.Enabled && x.Trigger == trigger))
        {
            if (!_ruleEngine.Evaluate(action.Conditions, context))
            {
                continue;
            }

            var state = await _store.GetActionScheduleStateAsync(occurrence.Id, action.Id, cancellationToken);
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
                        state.ExecutionCount + 1);

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
                        ExecutionNumber = state.ExecutionCount + 1,
                        CreatedAt = now,
                        Status = DeliveryStatus.Pending,
                        AttemptCount = 0,
                        DueAt = dueAt,
                        Fields = context.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)
                    });
                }
            }

            await _store.AddDeliveriesAsync(deliveries, cancellationToken);
        }
    }

    private async Task ResolveMissingRecordsAsync(
        AutomationDefinition automation,
        ISet<string> seenKeys,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (automation.MissingRecordBehavior != MissingRecordBehavior.Resolve)
        {
            return;
        }

        var open = await _store.GetOpenOccurrencesAsync(automation.Id, cancellationToken);
        foreach (var occurrence in open.Where(x => !seenKeys.Contains(x.RecordKey)))
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
        int execution)
    {
        var source = $"{occurrenceId:N}|{actionId:N}|{channelId:N}|{recipient.Trim().ToLowerInvariant()}|{execution}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }

    private sealed record MergedRecord(
        string Key,
        IReadOnlyDictionary<string, string?> Fields,
        string Fingerprint);
}
