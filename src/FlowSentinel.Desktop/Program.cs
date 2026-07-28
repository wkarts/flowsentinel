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
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, eventArgs) =>
            HandleUnexpectedException(eventArgs.Exception);

        ApplicationConfiguration.Initialize();
        RunAsync(args).GetAwaiter().GetResult();
    }

    private static void HandleUnexpectedException(Exception exception)
    {
        try
        {
            var paths = AppPaths.ForDesktop();
            paths.EnsureDirectories();
            var logPath = Path.Combine(paths.LogDirectory, "unhandled-ui.log");
            File.AppendAllText(
                logPath,
                $"[{DateTimeOffset.Now:O}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // A falha de gravação do log não deve ocultar o erro original.
        }

        MessageBox.Show(
            $"O FlowSentinel encontrou um erro inesperado, mas continuará aberto.\n\n{exception.Message}",
            "FlowSentinel - Erro inesperado",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
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
            System.Windows.Forms.Application.Run(context);
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
