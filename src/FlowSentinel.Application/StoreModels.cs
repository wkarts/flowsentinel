using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class AutomationStoreItem
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public bool Enabled { get; init; }
    public int IntervalSeconds { get; init; }
    public DateTimeOffset? LastRunAt { get; init; }
    public DateTimeOffset NextRunAt { get; init; }
    public string? LastError { get; init; }
}

public sealed class OccurrenceStoreItem
{
    public Guid Id { get; init; }
    public Guid AutomationId { get; init; }
    public required string RecordKey { get; init; }
    public OccurrenceStatus Status { get; set; }
    public DateTimeOffset OpenedAt { get; init; }
    public DateTimeOffset LastEvaluatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public required Dictionary<string, string?> Snapshot { get; set; }
    public required string Fingerprint { get; set; }
}

public sealed class DeliveryStoreItem
{
    public Guid Id { get; init; }
    public Guid OccurrenceId { get; init; }
    public Guid AutomationId { get; init; }
    public Guid ActionId { get; init; }
    public Guid ChannelConfigurationId { get; init; }
    public ChannelType ChannelType { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public required string IdempotencyKey { get; init; }
    public int ExecutionNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? LastError { get; set; }
    public required Dictionary<string, string?> Fields { get; init; }
}

public sealed class ActionScheduleState
{
    public int ExecutionCount { get; init; }
    public DateTimeOffset? LastScheduledAt { get; init; }
    public bool ConditionActive { get; init; }
    public int EpisodeNumber { get; init; }
}

public sealed class ActionRuntimeStateStoreItem
{
    public Guid OccurrenceId { get; init; }
    public Guid ActionId { get; init; }
    public required ActionScheduleState State { get; init; }
}

public sealed class AutomationExecutionHistoryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AutomationId { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public bool Success { get; init; }
    public int RecordCount { get; init; }
    public int ChangedRecordCount { get; init; }
    public string? Error { get; init; }
}

public sealed class RecordChangeHistoryItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AutomationId { get; init; }
    public Guid OccurrenceId { get; init; }
    public required string RecordKey { get; init; }
    public DateTimeOffset DetectedAt { get; init; }
    public required Dictionary<string, string?> PreviousSnapshot { get; init; }
    public required Dictionary<string, string?> CurrentSnapshot { get; init; }
    public required List<string> ChangedFields { get; init; }
}

public sealed class DashboardSnapshot
{
    public int EnabledAutomations { get; init; }
    public int ActiveOccurrences { get; init; }
    public int PendingDeliveries { get; init; }
    public int FailedDeliveries { get; init; }
    public DateTimeOffset? LastExecutionAt { get; init; }
}
