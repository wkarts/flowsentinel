using FlowSentinel.Application;
using FlowSentinel.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        RunAsync(args).GetAwaiter().GetResult();
    }

    private static async Task RunAsync(string[] args)
    {
        using var instance = new SingleInstanceGuard("Local\\FlowSentinel.Desktop");
        if (!instance.IsOwner)
        {
            MessageBox.Show(
                "O FlowSentinel já está em execução.",
                "FlowSentinel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var paths = AppPaths.ForDesktop();
        paths.EnsureDirectories();
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddFlowSentinelFileLogging(paths.LogDirectory);
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddSingleton<DesktopNotificationSink>();
        builder.Services.AddSingleton<ILocalNotificationSink>(provider =>
            provider.GetRequiredService<DesktopNotificationSink>());
        builder.Services.AddFlowSentinelInfrastructure(paths);
        builder.Services.AddFlowSentinelApplication();
        builder.Services.AddSingleton<MainForm>();
        builder.Services.AddSingleton<TrayApplicationContext>();

        using var host = builder.Build();
        try
        {
            await host.StartAsync();
            var context = host.Services.GetRequiredService<TrayApplicationContext>();
            Application.Run(context);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Não foi possível iniciar o FlowSentinel.\n\n{exception}",
                "Erro de inicialização",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            await host.StopAsync(TimeSpan.FromSeconds(10));
        }
    }
}
