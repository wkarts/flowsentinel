namespace FlowSentinel.Application;

public interface IWorkerRuntimeSettings
{
    bool AutomationSchedulerEnabled { get; }
    int AutomationSchedulerPollingSeconds { get; }
    bool DeliveryDispatcherEnabled { get; }
    int DeliveryDispatcherPollingSeconds { get; }
    int MaxDeliveriesPerCycle { get; }
    int MaxParallelDeliveries { get; }
}

public sealed class DefaultWorkerRuntimeSettings : IWorkerRuntimeSettings
{
    public bool AutomationSchedulerEnabled => true;
    public int AutomationSchedulerPollingSeconds => 5;
    public bool DeliveryDispatcherEnabled => true;
    public int DeliveryDispatcherPollingSeconds => 2;
    public int MaxDeliveriesPerCycle => 50;
    public int MaxParallelDeliveries => 8;
}
