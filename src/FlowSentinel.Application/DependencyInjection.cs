using Microsoft.Extensions.DependencyInjection;

namespace FlowSentinel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFlowSentinelApplication(this IServiceCollection services)
    {
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<ITemplateRenderer, TemplateRenderer>();
        services.AddSingleton<IRecipientResolver, RecipientResolver>();
        services.AddSingleton<IAutomationExecutor, AutomationExecutor>();
        services.AddSingleton<IAutomationControl>(provider =>
            (IAutomationControl)provider.GetRequiredService<IAutomationExecutor>());
        services.AddHostedService<AutomationSchedulerWorker>();
        services.AddHostedService<DeliveryDispatcherWorker>();
        return services;
    }
}
