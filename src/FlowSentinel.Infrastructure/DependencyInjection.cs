using FlowSentinel.Application;
using FlowSentinel.Infrastructure.Channels;
using FlowSentinel.Infrastructure.Persistence;
using FlowSentinel.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FlowSentinel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFlowSentinelInfrastructure(
        this IServiceCollection services,
        AppPaths paths)
    {
        paths.EnsureDirectories();
        services.AddSingleton(paths);
        services.AddDbContextFactory<FlowSentinelDbContext>(options =>
        {
            options.UseSqlite($"Data Source={paths.DatabasePath};Cache=Shared;Pooling=True;Foreign Keys=True;Default Timeout=15");
        });
        services.AddSingleton<IFlowStore, FlowStore>();
        services.AddSingleton<IContactDirectory, JsonContactDirectory>();
        services.AddSingleton<ISecretProtector, WindowsDpapiSecretProtector>();
        services.TryAddSingleton<ILocalNotificationSink, NullLocalNotificationSink>();

        services.AddSingleton<IDataSourceReader, ExcelSourceReader>();
        services.AddSingleton<IDataSourceReader, CsvSourceReader>();
        services.AddSingleton<IDataSourceReader, TextSourceReader>();
        services.AddSingleton<IDataSourceReader, DatabaseSourceReader>();
        services.AddSingleton<ISourceDesignerService, SourceDesignerService>();
        services.AddSingleton<IWorkbookMonitoringService, WorkbookMonitoringService>();

        services.AddHttpClient(nameof(TelegramChannel));
        services.AddHttpClient(nameof(EvolutionApiChannel));
        services.AddSingleton<INotificationChannel, LocalWindowsChannel>();
        services.AddSingleton<INotificationChannel, TelegramChannel>();
        services.AddSingleton<EvolutionApiChannel>();
        services.AddSingleton<INotificationChannel>(provider => provider.GetRequiredService<EvolutionApiChannel>());
        services.AddSingleton<IEvolutionInstanceService>(provider => provider.GetRequiredService<EvolutionApiChannel>());
        services.AddSingleton<INotificationChannel, EmailChannel>();
        return services;
    }
}
