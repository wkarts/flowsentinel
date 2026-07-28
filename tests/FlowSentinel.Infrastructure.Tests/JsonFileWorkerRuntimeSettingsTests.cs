using FlowSentinel.Infrastructure;

namespace FlowSentinel.Infrastructure.Tests;

public sealed class JsonFileWorkerRuntimeSettingsTests
{
    [Fact]
    public void ReadsAndNormalizesServiceRuntimeSettings()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"FlowSentinel.Service.Settings.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "service-settings.json");

        try
        {
            File.WriteAllText(path, """
            {
              "automationSchedulerEnabled": false,
              "automationSchedulerPollingSeconds": 0,
              "deliveryDispatcherEnabled": true,
              "deliveryDispatcherPollingSeconds": 9,
              "maxDeliveriesPerCycle": 2500,
              "maxParallelDeliveries": 12
            }
            """);

            var settings = new JsonFileWorkerRuntimeSettings(path);

            Assert.False(settings.AutomationSchedulerEnabled);
            Assert.Equal(1, settings.AutomationSchedulerPollingSeconds);
            Assert.True(settings.DeliveryDispatcherEnabled);
            Assert.Equal(9, settings.DeliveryDispatcherPollingSeconds);
            Assert.Equal(1000, settings.MaxDeliveriesPerCycle);
            Assert.Equal(12, settings.MaxParallelDeliveries);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
