using FlowSentinel.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Application;

public sealed class AutomationSchedulerWorker : BackgroundService
{
    private static readonly TimeSpan InitialStartupDelay = TimeSpan.FromSeconds(2);
    private readonly IFlowStore _store;
    private readonly IAutomationExecutor _executor;
    private readonly IWorkerRuntimeSettings _settings;
    private readonly ILogger<AutomationSchedulerWorker> _logger;

    public AutomationSchedulerWorker(
        IFlowStore store,
        IAutomationExecutor executor,
        IWorkerRuntimeSettings settings,
        ILogger<AutomationSchedulerWorker> logger)
    {
        _store = store;
        _executor = executor;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _store.InitializeAsync(stoppingToken);
        await Task.Delay(InitialStartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_settings.AutomationSchedulerEnabled)
                {
                    var due = await _store.GetDueAutomationsAsync(DateTimeOffset.Now, stoppingToken);
                    await Task.WhenAll(due.Select(x => _executor.ExecuteAsync(x.Id, stoppingToken)));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falha no agendador de automações.");
            }

            await DelayAsync(_settings.AutomationSchedulerPollingSeconds, stoppingToken);
        }
    }

    private static Task DelayAsync(int seconds, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 3600)), cancellationToken);
}

public sealed class DeliveryDispatcherWorker : BackgroundService
{
    private static readonly TimeSpan InitialStartupDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];

    private readonly IFlowStore _store;
    private readonly IReadOnlyDictionary<ChannelType, INotificationChannel> _channels;
    private readonly IWorkerRuntimeSettings _settings;
    private readonly ILogger<DeliveryDispatcherWorker> _logger;

    public DeliveryDispatcherWorker(
        IFlowStore store,
        IEnumerable<INotificationChannel> channels,
        IWorkerRuntimeSettings settings,
        ILogger<DeliveryDispatcherWorker> logger)
    {
        _store = store;
        _channels = channels.ToDictionary(x => x.ChannelType);
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _store.InitializeAsync(stoppingToken);
        await Task.Delay(InitialStartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_settings.DeliveryDispatcherEnabled)
                {
                    var batchSize = Math.Clamp(_settings.MaxDeliveriesPerCycle, 1, 1000);
                    var deliveries = await _store.ClaimDueDeliveriesAsync(
                        DateTimeOffset.Now,
                        batchSize,
                        stoppingToken);

                    var batches = await BuildDispatchBatchesAsync(deliveries, stoppingToken);
                    await Parallel.ForEachAsync(
                        batches,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Clamp(_settings.MaxParallelDeliveries, 1, 64),
                            CancellationToken = stoppingToken
                        },
                        DispatchBatchAsync);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Falha no processador da fila de notificações.");
            }

            await DelayAsync(_settings.DeliveryDispatcherPollingSeconds, stoppingToken);
        }
    }

    private async Task<IReadOnlyList<DispatchBatch>> BuildDispatchBatchesAsync(
        IReadOnlyCollection<DeliveryStoreItem> deliveries,
        CancellationToken cancellationToken)
    {
        if (deliveries.Count == 0)
        {
            return [];
        }

        var definitions = new Dictionary<Guid, AutomationDefinition?>();
        foreach (var automationId in deliveries.Select(x => x.AutomationId).Distinct())
        {
            definitions[automationId] = await _store.GetAutomationDefinitionAsync(automationId, cancellationToken);
        }

        var candidates = deliveries.Select(delivery =>
        {
            definitions.TryGetValue(delivery.AutomationId, out var automation);
            var action = automation?.Actions.FirstOrDefault(x => x.Id == delivery.ActionId);
            var channelDefinition = action?.Channels.FirstOrDefault(x =>
                x.ChannelConfigurationId == delivery.ChannelConfigurationId &&
                x.ChannelType == delivery.ChannelType);

            var mode = delivery.ChannelType == ChannelType.LocalWindows
                ? NotificationGroupingMode.Individual
                : channelDefinition?.GroupingMode ?? NotificationGroupingMode.Individual;
            var entityKey = mode == NotificationGroupingMode.ByEntity
                ? NotificationBatchComposer.ResolveEntityKey(delivery.Fields, channelDefinition?.GroupField)
                : string.Empty;
            if (mode == NotificationGroupingMode.ByEntity && string.IsNullOrWhiteSpace(entityKey))
            {
                mode = NotificationGroupingMode.Individual;
            }

            var groupingKey = mode switch
            {
                NotificationGroupingMode.SingleMessage => "summary",
                NotificationGroupingMode.ByEntity => $"entity:{entityKey}",
                _ => $"individual:{delivery.Id:N}"
            };

            return new DispatchCandidate(
                delivery,
                automation,
                channelDefinition,
                mode,
                $"{delivery.AutomationId:N}|{delivery.ChannelConfigurationId:N}|{delivery.Recipient.Trim().ToLowerInvariant()}|{mode}|{groupingKey}");
        }).ToArray();

        return candidates
            .GroupBy(x => x.GroupKey, StringComparer.Ordinal)
            .Select(group => new DispatchBatch(
                group.First().Automation,
                group.First().ChannelDefinition,
                group.First().Mode,
                group.Select(x => x.Delivery).OrderBy(x => x.CreatedAt).ToArray()))
            .ToArray();
    }

    private async ValueTask DispatchBatchAsync(DispatchBatch batch, CancellationToken cancellationToken)
    {
        var first = batch.Deliveries[0];
        try
        {
            if (!_channels.TryGetValue(first.ChannelType, out var channel))
            {
                await CompleteBatchAsync(
                    batch,
                    DeliveryResult.Failed($"Canal {first.ChannelType} não registrado.", transient: false),
                    cancellationToken);
                return;
            }

            var configuration = await _store.GetChannelConfigurationAsync(
                first.ChannelConfigurationId,
                cancellationToken);

            if (configuration is null || !configuration.Enabled || configuration.Type != first.ChannelType)
            {
                await CompleteBatchAsync(
                    batch,
                    DeliveryResult.Skipped("Canal removido, desabilitado ou incompatível; entrega ignorada sem registrar falha operacional."),
                    cancellationToken);
                return;
            }

            var automationName = batch.Automation?.Name ?? first.AutomationId.ToString("N");
            var content = NotificationBatchComposer.Compose(automationName, batch.Mode, batch.Deliveries);
            var fields = first.Fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            fields["BatchCount"] = batch.Deliveries.Count.ToString();
            fields["BatchMode"] = batch.Mode.ToString();

            var request = new DeliveryRequest
            {
                DeliveryId = first.Id,
                OccurrenceId = first.OccurrenceId,
                AutomationName = automationName,
                ActionName = batch.Automation?.Actions.FirstOrDefault(x => x.Id == first.ActionId)?.Name ?? first.ActionId.ToString("N"),
                Recipient = first.Recipient,
                Subject = content.Subject,
                Message = content.Message,
                Fields = fields
            };

            var result = await channel.SendAsync(configuration, request, cancellationToken);
            await CompleteBatchAsync(batch, result, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Falha inesperada no lote de {Count} entrega(s), canal {ChannelType}.",
                batch.Deliveries.Count,
                first.ChannelType);
            await CompleteBatchAsync(batch, DeliveryResult.Failed(exception.Message), cancellationToken);
        }
    }

    private async Task CompleteBatchAsync(
        DispatchBatch batch,
        DeliveryResult result,
        CancellationToken cancellationToken)
    {
        foreach (var delivery in batch.Deliveries)
        {
            await _store.CompleteDeliveryAsync(
                delivery.Id,
                result,
                GetNextAttempt(delivery, result),
                cancellationToken);
        }
    }

    private static DateTimeOffset? GetNextAttempt(DeliveryStoreItem delivery, DeliveryResult result)
    {
        if (result.Success || result.IsSkipped || !result.IsTransient)
        {
            return null;
        }

        var index = Math.Max(0, delivery.AttemptCount - 1);
        return index >= RetryDelays.Length
            ? null
            : DateTimeOffset.Now.Add(RetryDelays[index]);
    }

    private static Task DelayAsync(int seconds, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 3600)), cancellationToken);

    private sealed record DispatchCandidate(
        DeliveryStoreItem Delivery,
        AutomationDefinition? Automation,
        ActionChannelDefinition? ChannelDefinition,
        NotificationGroupingMode Mode,
        string GroupKey);

    private sealed record DispatchBatch(
        AutomationDefinition? Automation,
        ActionChannelDefinition? ChannelDefinition,
        NotificationGroupingMode Mode,
        IReadOnlyList<DeliveryStoreItem> Deliveries);
}
