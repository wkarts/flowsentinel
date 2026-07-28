using System.Text.Json;
using FlowSentinel.Application;

namespace FlowSentinel.Infrastructure;

public sealed class JsonFileWorkerRuntimeSettings : IWorkerRuntimeSettings
{
    private readonly object _sync = new();
    private readonly string _path;
    private DateTime _lastWriteUtc;
    private WorkerRuntimeFileModel _current = new();

    public JsonFileWorkerRuntimeSettings(string path)
    {
        _path = Path.GetFullPath(path);
    }

    public bool AutomationSchedulerEnabled => Read().AutomationSchedulerEnabled;
    public int AutomationSchedulerPollingSeconds => Read().AutomationSchedulerPollingSeconds;
    public bool DeliveryDispatcherEnabled => Read().DeliveryDispatcherEnabled;
    public int DeliveryDispatcherPollingSeconds => Read().DeliveryDispatcherPollingSeconds;
    public int MaxDeliveriesPerCycle => Read().MaxDeliveriesPerCycle;
    public int MaxParallelDeliveries => Read().MaxParallelDeliveries;

    private WorkerRuntimeFileModel Read()
    {
        lock (_sync)
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return _current;
                }

                var lastWrite = File.GetLastWriteTimeUtc(_path);
                if (lastWrite == _lastWriteUtc)
                {
                    return _current;
                }

                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<WorkerRuntimeFileModel>(json, JsonOptions);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    _current = loaded;
                    _lastWriteUtc = lastWrite;
                }
            }
            catch
            {
                // Mantém os últimos parâmetros válidos em caso de escrita parcial ou arquivo inválido.
            }

            return _current;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private sealed class WorkerRuntimeFileModel
    {
        public bool AutomationSchedulerEnabled { get; set; } = true;
        public int AutomationSchedulerPollingSeconds { get; set; } = 5;
        public bool DeliveryDispatcherEnabled { get; set; } = true;
        public int DeliveryDispatcherPollingSeconds { get; set; } = 2;
        public int MaxDeliveriesPerCycle { get; set; } = 50;
        public int MaxParallelDeliveries { get; set; } = 8;

        public void Normalize()
        {
            AutomationSchedulerPollingSeconds = Math.Clamp(AutomationSchedulerPollingSeconds, 1, 3600);
            DeliveryDispatcherPollingSeconds = Math.Clamp(DeliveryDispatcherPollingSeconds, 1, 3600);
            MaxDeliveriesPerCycle = Math.Clamp(MaxDeliveriesPerCycle, 1, 1000);
            MaxParallelDeliveries = Math.Clamp(MaxParallelDeliveries, 1, 64);
        }
    }
}
