using System.Diagnostics;
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

        var startupWatch = Stopwatch.StartNew();
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
            splash.UpdateStatus("Carregando preferências locais", 10, "Validando diretórios, inicialização automática e parâmetros do Desktop.");
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

        splash?.UpdateStatus("Montando os serviços da aplicação", 30, "Registrando banco local, leitores de fontes, regras, canais e processadores em segundo plano.");

        using var host = builder.Build();
        try
        {
            splash?.UpdateStatus("Iniciando os processadores", 50, "Ativando o agendador de automações e a fila de notificações.");
            await host.StartAsync();

            splash?.UpdateStatus("Verificando o banco local", 68, "Inicializando a estrutura de dados e validando a compatibilidade do armazenamento.");
            var store = host.Services.GetRequiredService<IFlowStore>();
            await store.InitializeAsync(CancellationToken.None);

            splash?.UpdateStatus("Carregando automações e canais", 80, "Lendo configurações, automações ativas e canais disponíveis.");
            var automations = await store.GetAutomationsAsync(CancellationToken.None);
            var channels = await store.GetChannelConfigurationsAsync(CancellationToken.None);

            splash?.UpdateStatus("Carregando contatos e grupos", 89, "Validando o catálogo reutilizável de destinatários e suas permissões.");
            var contactDirectory = host.Services.GetRequiredService<IContactDirectory>();
            var contacts = await contactDirectory.GetSnapshotAsync(CancellationToken.None);

            splash?.UpdateStatus(
                "Preparando a central de monitoramento",
                96,
                $"{automations.Count} automação(ões), {channels.Count} canal(is), {contacts.Contacts.Count} contato(s) e {contacts.Groups.Count} grupo(s) carregado(s).");
            var context = host.Services.GetRequiredService<TrayApplicationContext>();

            if (splash is not null)
            {
                var minimumVisibility = TimeSpan.FromMilliseconds(2500);
                var remaining = minimumVisibility - startupWatch.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining);
                }
                splash.UpdateStatus("FlowSentinel pronto", 100, "A central de monitoramento foi carregada com sucesso.");
                await Task.Delay(300);
                splash.Close();
                splash.Dispose();
                splash = null;
            }

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