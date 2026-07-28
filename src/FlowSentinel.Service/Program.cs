using FlowSentinel.Application;
using FlowSentinel.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Service;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        using var guard = new SingleInstanceGuard(@"Global\FlowSentinel.Service");
        if (!guard.IsOwner)
        {
            return;
        }

        var paths = AppPaths.ForService();
        paths.EnsureDirectories();

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "FlowSentinel";
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddFlowSentinelFileLogging(paths.LogDirectory);
        builder.Services.AddSingleton<IWorkerRuntimeSettings>(
            new JsonFileWorkerRuntimeSettings(Path.Combine(paths.RootDirectory, "service-settings.json")));
        builder.Services
            .AddFlowSentinelApplication()
            .AddFlowSentinelInfrastructure(paths);

        await builder.Build().RunAsync();
    }
}
