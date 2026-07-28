using FlowSentinel.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Application;

public sealed class AutomationSchedulerWorker : BackgroundService
{
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

                    await Parallel.ForEachAsync(
                        deliveries,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Clamp(_settings.MaxParallelDeliveries, 1, 64),
                            CancellationToken = stoppingToken
                        },
                        DispatchAsync);
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

    private async ValueTask DispatchAsync(DeliveryStoreItem delivery, CancellationToken cancellationToken)
    {
        try
        {
            if (!_channels.TryGetValue(delivery.ChannelType, out var channel))
            {
                await _store.CompleteDeliveryAsync(
                    delivery.Id,
                    DeliveryResult.Failed($"Canal {delivery.ChannelType} não registrado.", transient: false),
                    null,
                    cancellationToken);
                return;
            }

            var configuration = await _store.GetChannelConfigurationAsync(
                delivery.ChannelConfigurationId,
                cancellationToken);

            if (configuration is null || !configuration.Enabled)
            {
                await _store.CompleteDeliveryAsync(
                    delivery.Id,
                    DeliveryResult.Failed("Configuração de canal inexistente ou desabilitada.", transient: false),
                    null,
                    cancellationToken);
                return;
            }

            var request = new DeliveryRequest
            {
                DeliveryId = delivery.Id,
                OccurrenceId = delivery.OccurrenceId,
                AutomationName = delivery.AutomationId.ToString("N"),
                ActionName = delivery.ActionId.ToString("N"),
                Recipient = delivery.Recipient,
                Subject = delivery.Subject,
                Message = delivery.Message,
                Fields = delivery.Fields
            };

            var result = await channel.SendAsync(configuration, request, cancellationToken);
            var nextAttempt = GetNextAttempt(delivery, result);
            await _store.CompleteDeliveryAsync(delivery.Id, result, nextAttempt, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha inesperada na entrega {DeliveryId}.", delivery.Id);
            var result = DeliveryResult.Failed(exception.Message);
            await _store.CompleteDeliveryAsync(
                delivery.Id,
                result,
                GetNextAttempt(delivery, result),
                cancellationToken);
        }
    }

    private static DateTimeOffset? GetNextAttempt(DeliveryStoreItem delivery, DeliveryResult result)
    {
        if (result.Success || !result.IsTransient)
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
}
