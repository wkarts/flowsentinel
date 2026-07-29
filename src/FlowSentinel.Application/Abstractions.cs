using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public interface IDataSourceReader
{
    SourceType SourceType { get; }
    Task<SourceReadResult> ReadAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken);
}

public interface IRuleEngine
{
    bool Evaluate(RuleSetDefinition? ruleSet, EvaluationContext context, bool defaultValue = true);
}

public interface ITemplateRenderer
{
    string Render(string template, EvaluationContext context);
}

public interface IRecipientResolver
{
    Task<IReadOnlyCollection<ResolvedRecipient>> ResolveAsync(
        AutomationDefinition automation,
        ActionDefinition action,
        ChannelType channelType,
        EvaluationContext context,
        CancellationToken cancellationToken);
}

public interface IContactDirectory
{
    Task<ContactDirectoryDefinition> GetSnapshotAsync(CancellationToken cancellationToken);
    Task SaveAsync(ContactDirectoryDefinition definition, CancellationToken cancellationToken);
}

public interface INotificationChannel
{
    ChannelType ChannelType { get; }
    Task<DeliveryResult> SendAsync(
        ChannelConfiguration configuration,
        DeliveryRequest request,
        CancellationToken cancellationToken);
}

public interface ILocalNotificationSink
{
    Task ShowAsync(string title, string message, CancellationToken cancellationToken);
}

public interface ISecretProtector
{
    string Protect(string plainText, SecretProtectionScope scope = SecretProtectionScope.CurrentUser);
    string Unprotect(string protectedText);
    string UnprotectIfNeeded(string value);
}

public interface IFlowStore
{
    Task InitializeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AutomationStoreItem>> GetAutomationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AutomationStoreItem>> GetDueAutomationsAsync(DateTimeOffset now, CancellationToken cancellationToken);
    Task<AutomationDefinition?> GetAutomationDefinitionAsync(Guid automationId, CancellationToken cancellationToken);
    Task SaveAutomationAsync(AutomationDefinition definition, CancellationToken cancellationToken);
    Task DeleteAutomationAsync(Guid automationId, CancellationToken cancellationToken);
    Task MarkAutomationExecutionAsync(Guid automationId, DateTimeOffset nextRunAt, string? error, CancellationToken cancellationToken);
    Task AddAutomationExecutionHistoryAsync(AutomationExecutionHistoryItem history, CancellationToken cancellationToken);
    Task AddRecordChangeHistoryAsync(RecordChangeHistoryItem history, CancellationToken cancellationToken);

    Task<OccurrenceStoreItem?> GetOpenOccurrenceAsync(Guid automationId, string recordKey, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OccurrenceStoreItem>> GetOpenOccurrencesAsync(Guid automationId, CancellationToken cancellationToken);
    Task CreateOccurrenceAsync(OccurrenceStoreItem occurrence, CancellationToken cancellationToken);
    Task UpdateOccurrenceAsync(OccurrenceStoreItem occurrence, CancellationToken cancellationToken);

    Task<ActionScheduleState> GetActionScheduleStateAsync(Guid occurrenceId, Guid actionId, CancellationToken cancellationToken);
    Task<ActionScheduleState> UpdateActionConditionStateAsync(
        Guid occurrenceId,
        Guid actionId,
        bool conditionActive,
        bool resetOnReentry,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken);
    Task MarkActionScheduledAsync(
        Guid occurrenceId,
        Guid actionId,
        int episodeNumber,
        int executionNumber,
        DateTimeOffset scheduledAt,
        CancellationToken cancellationToken);
    Task AddDeliveriesAsync(IReadOnlyCollection<DeliveryStoreItem> deliveries, CancellationToken cancellationToken);
    Task CancelPendingDeliveriesAsync(Guid occurrenceId, CancellationToken cancellationToken);
    Task CancelPendingDeliveriesAsync(Guid occurrenceId, Guid actionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<DeliveryStoreItem>> ClaimDueDeliveriesAsync(DateTimeOffset now, int take, CancellationToken cancellationToken);
    Task CompleteDeliveryAsync(Guid deliveryId, DeliveryResult result, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken);

    Task<ChannelConfiguration?> GetChannelConfigurationAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ChannelConfiguration>> GetChannelConfigurationsAsync(CancellationToken cancellationToken);
    Task SaveChannelConfigurationAsync(ChannelConfiguration configuration, CancellationToken cancellationToken);
    Task DeleteChannelConfigurationAsync(Guid id, CancellationToken cancellationToken);

    Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken);
}

public interface IAutomationExecutor
{
    Task ExecuteAsync(Guid automationId, CancellationToken cancellationToken);
}

public interface IAutomationControl
{
    Task ExecuteNowAsync(Guid automationId, CancellationToken cancellationToken);
}
