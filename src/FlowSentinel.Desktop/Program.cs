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
                "O FlowSentinel já está em execução na bandeja do Windows.",
                "FlowSentinel",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var paths = AppPaths.ForDesktop();
        paths.EnsureDirectories();
        var settingsService = new DesktopSettingsService(paths);
        settingsService.EnsureSaved();
        var settings = settingsService.Current;
        var launchOptions = DesktopLaunchOptions.Parse(args, settings);

        try
        {
            StartupRegistration.SetEnabled(settings.StartWithWindows, settings.StartMinimizedToTray);
        }
        catch
        {
            // O programa continua mesmo quando uma política do Windows bloqueia o registro de inicialização.
        }

        SplashForm? splash = null;
        var shouldShowSplash = settings.ShowSplashScreen &&
                               !launchOptions.SuppressSplash &&
                               (!launchOptions.IsWindowsStartup || settings.ShowSplashOnWindowsStartup);

        if (shouldShowSplash)
        {
            splash = new SplashForm();
            splash.Show();
            splash.UpdateStatus("Carregando preferências locais...");
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddFlowSentinelFileLogging(paths.LogDirectory);
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddSingleton(settingsService);
        builder.Services.AddSingleton<IWorkerRuntimeSettings>(settingsService);
        builder.Services.AddSingleton(launchOptions);
        builder.Services.AddSingleton<WindowsServiceManager>();
        builder.Services.AddSingleton<DesktopNotificationSink>();
        builder.Services.AddSingleton<ILocalNotificationSink>(provider =>
            provider.GetRequiredService<DesktopNotificationSink>());
        builder.Services.AddFlowSentinelInfrastructure(paths);
        builder.Services.AddFlowSentinelApplication();
        builder.Services.AddSingleton<MainForm>();
        builder.Services.AddSingleton<TrayApplicationContext>();

        splash?.UpdateStatus("Inicializando banco, regras e canais...");

        using var host = builder.Build();
        try
        {
            await host.StartAsync();
            splash?.UpdateStatus("Preparando a bandeja do Windows...");
            var context = host.Services.GetRequiredService<TrayApplicationContext>();
            splash?.Close();
            splash?.Dispose();
            splash = null;
            System.Windows.Forms.Application.Run(context);
        }
        catch (Exception exception)
        {
            splash?.Close();
            MessageBox.Show(
                $"Não foi possível iniciar o FlowSentinel.\n\n{exception}",
                "Erro de inicialização",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            splash?.Dispose();
            await host.StopAsync(TimeSpan.FromSeconds(10));
        }
    }
}
