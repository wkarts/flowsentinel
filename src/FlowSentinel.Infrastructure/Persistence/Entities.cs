using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Persistence;

internal sealed class AutomationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; }
    public int Priority { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset NextRunAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

internal sealed class OccurrenceEntity
{
    public Guid Id { get; set; }
    public Guid AutomationId { get; set; }
    public string RecordKey { get; set; } = string.Empty;
    public OccurrenceStatus Status { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset LastEvaluatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string Fingerprint { get; set; } = string.Empty;
}

internal sealed class DeliveryEntity
{
    public Guid Id { get; set; }
    public Guid OccurrenceId { get; set; }
    public Guid AutomationId { get; set; }
    public Guid ActionId { get; set; }
    public Guid ChannelConfigurationId { get; set; }
    public ChannelType ChannelType { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public int ExecutionNumber { get; set; }
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? LastError { get; set; }
    public string FieldsJson { get; set; } = "{}";
}

internal sealed class ChannelConfigurationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChannelType Type { get; set; }
    public bool Enabled { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
