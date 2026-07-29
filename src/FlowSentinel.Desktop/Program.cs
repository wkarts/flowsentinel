using System.Diagnostics;
using FlowSentinel.Application;
using FlowSentinel.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FlowSentinel.Desktop;

internal static class Program
{
    private static readonly TimeSpan DatabaseStartupTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CatalogStartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan HostStartupTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MinimumSplashVisibility = TimeSpan.FromMilliseconds(1800);
    private static readonly TimeSpan SplashProgressRefreshInterval = TimeSpan.FromMilliseconds(250);

    [STAThread]
    private static void Main(string[] args)
    {
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, eventArgs) =>
            HandleUnexpectedException(eventArgs.Exception);

        ApplicationConfiguration.Initialize();
        Run(args);
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

    private static void Run(string[] args)
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
            splash.UpdateStatus(
                "Carregando preferências locais",
                10,
                "Validando diretórios, inicialização automática e parâmetros do Desktop.");
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

        splash?.UpdateStatus(
            "Montando os serviços da aplicação",
            30,
            "Registrando banco local, leitores de fontes, regras, canais e processadores em segundo plano.");

        using var host = builder.Build();
        var startupLogger = host.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("FlowSentinel.Desktop.Startup");
        var hostStarted = false;

        try
        {
            RunStartupStep(
                splash,
                startupLogger,
                "Verificando o banco local",
                58,
                "Inicializando a estrutura de dados e aplicando atualizações incrementais sem apagar o histórico.",
                DatabaseStartupTimeout,
                cancellationToken => host.Services
                    .GetRequiredService<IFlowStore>()
                    .InitializeAsync(cancellationToken));

            var snapshot = RunStartupStep(
                splash,
                startupLogger,
                "Carregando automações, canais e contatos",
                78,
                "Lendo somente os dados necessários para preparar a interface principal.",
                CatalogStartupTimeout,
                async cancellationToken =>
                {
                    var store = host.Services.GetRequiredService<IFlowStore>();
                    var contactDirectory = host.Services.GetRequiredService<IContactDirectory>();
                    var automationsTask = store.GetAutomationsAsync(cancellationToken);
                    var channelsTask = store.GetChannelConfigurationsAsync(cancellationToken);
                    var contactsTask = contactDirectory.GetSnapshotAsync(cancellationToken);
                    await Task.WhenAll(automationsTask, channelsTask, contactsTask).ConfigureAwait(false);
                    return new StartupSnapshot(
                        automationsTask.Result.Count,
                        channelsTask.Result.Count,
                        contactsTask.Result.Contacts.Count,
                        contactsTask.Result.Groups.Count);
                });

            splash?.UpdateStatus(
                "Preparando a central de monitoramento",
                88,
                $"{snapshot.AutomationCount} automação(ões), {snapshot.ChannelCount} canal(is), " +
                $"{snapshot.ContactCount} contato(s) e {snapshot.GroupCount} grupo(s) carregado(s).");

            // Os controles WinForms são construídos exclusivamente na thread STA principal.
            var context = host.Services.GetRequiredService<TrayApplicationContext>();

            RunStartupStep(
                splash,
                startupLogger,
                "Iniciando os processadores",
                95,
                "Ativando o agendador de automações e a fila de notificações em segundo plano.",
                HostStartupTimeout,
                cancellationToken => host.StartAsync(cancellationToken));
            hostStarted = true;

            if (splash is not null)
            {
                var remaining = MinimumSplashVisibility - startupWatch.Elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    WaitWithMessagePump(Task.Delay(remaining), splash, null, remaining.Add(TimeSpan.FromSeconds(2)));
                }

                splash.UpdateStatus("FlowSentinel pronto", 100, "A central de monitoramento foi carregada com sucesso.");
                WaitWithMessagePump(Task.Delay(180), splash, null, TimeSpan.FromSeconds(2));
                splash.Close();
                splash.Dispose();
                splash = null;
            }

            // O message loop permanece na mesma thread STA que criou todos os formulários.
            System.Windows.Forms.Application.Run(context);
        }
        catch (Exception exception)
        {
            startupLogger.LogError(exception, "Não foi possível concluir a inicialização do FlowSentinel.");
            splash?.Close();
            MessageBox.Show(
                "Não foi possível iniciar o FlowSentinel. A inicialização foi interrompida para evitar " +
                "que a aplicação permanecesse travada indefinidamente.\n\n" +
                $"{exception.Message}\n\nConsulte os logs em:\n{paths.LogDirectory}",
                "Erro de inicialização",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            splash?.Dispose();
            if (hostStarted)
            {
                try
                {
                    using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    host.StopAsync(shutdown.Token).GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    startupLogger.LogWarning(exception, "O host não encerrou dentro do prazo esperado.");
                }
            }
        }
    }

    private static void RunStartupStep(
        SplashForm? splash,
        ILogger logger,
        string stepName,
        int progress,
        string detail,
        TimeSpan timeout,
        Func<CancellationToken, Task> operation)
    {
        RunStartupStep<object?>(
            splash,
            logger,
            stepName,
            progress,
            detail,
            timeout,
            async cancellationToken =>
            {
                await operation(cancellationToken).ConfigureAwait(false);
                return null;
            });
    }

    private static T RunStartupStep<T>(
        SplashForm? splash,
        ILogger logger,
        string stepName,
        int progress,
        string detail,
        TimeSpan timeout,
        Func<CancellationToken, Task<T>> operation)
    {
        splash?.UpdateStatus(stepName, progress, detail);
        var watch = Stopwatch.StartNew();
        using var cancellation = new CancellationTokenSource();
        var task = Task.Run(() => operation(cancellation.Token), cancellation.Token);

        try
        {
            WaitWithMessagePump(task, splash, stepName, timeout, cancellation, watch);
            var result = task.GetAwaiter().GetResult();
            logger.LogInformation("Etapa de inicialização '{StartupStep}' concluída em {ElapsedMilliseconds} ms.",
                stepName,
                watch.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (watch.Elapsed >= timeout)
        {
            cancellation.Cancel();
            throw new TimeoutException(
                $"A etapa '{stepName}' excedeu o limite de {timeout.TotalSeconds:N0} segundos.");
        }
        catch
        {
            cancellation.Cancel();
            throw;
        }
    }

    private static void WaitWithMessagePump(
        Task task,
        SplashForm? splash,
        string? stepName,
        TimeSpan timeout,
        CancellationTokenSource? cancellation = null,
        Stopwatch? watch = null)
    {
        watch ??= Stopwatch.StartNew();
        TimeSpan? lastProgressRefresh = null;

        while (!task.IsCompleted)
        {
            System.Windows.Forms.Application.DoEvents();

            var elapsed = watch.Elapsed;
            if (ShouldRefreshSplashProgress(elapsed, lastProgressRefresh))
            {
                splash?.UpdateElapsed(stepName, elapsed, timeout);
                lastProgressRefresh = elapsed;
            }

            if (elapsed >= timeout)
            {
                cancellation?.Cancel();
                throw new TimeoutException(
                    stepName is null
                        ? $"A operação excedeu o limite de {timeout.TotalSeconds:N0} segundos."
                        : $"A etapa '{stepName}' excedeu o limite de {timeout.TotalSeconds:N0} segundos.");
            }

            Thread.Sleep(15);
        }

        task.GetAwaiter().GetResult();
    }

    internal static bool ShouldRefreshSplashProgress(TimeSpan elapsed, TimeSpan? lastProgressRefresh)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }

        if (lastProgressRefresh is null)
        {
            return true;
        }

        if (lastProgressRefresh.Value < TimeSpan.Zero || lastProgressRefresh.Value > elapsed)
        {
            throw new ArgumentOutOfRangeException(nameof(lastProgressRefresh));
        }

        return elapsed - lastProgressRefresh.Value >= SplashProgressRefreshInterval;
    }

    private sealed record StartupSnapshot(
        int AutomationCount,
        int ChannelCount,
        int ContactCount,
        int GroupCount);
}
