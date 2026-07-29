namespace FlowSentinel.Domain;

public sealed class DataRecord
{
    public string Key { get; init; } = string.Empty;
    public string SourceAlias { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Fields { get; init; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset CollectedAt { get; init; } = DateTimeOffset.UtcNow;
    public string Fingerprint { get; init; } = string.Empty;
}

public sealed class EvaluationContext
{
    public required AutomationDefinition Automation { get; init; }
    public required string RecordKey { get; init; }
    public required IReadOnlyDictionary<string, string?> Fields { get; init; }
    public IReadOnlyDictionary<string, string?> PreviousFields { get; init; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;
}

public sealed class ResolvedRecipient
{
    public required ChannelType ChannelType { get; init; }
    public required string Address { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class ChannelConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ChannelType Type { get; set; }
    public bool Enabled { get; set; } = true;
    public string SettingsJson { get; set; } = "{}";
}

public sealed class DeliveryRequest
{
    public required Guid DeliveryId { get; init; }
    public required Guid OccurrenceId { get; init; }
    public required string AutomationName { get; init; }
    public required string ActionName { get; init; }
    public required string Recipient { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public IReadOnlyDictionary<string, string?> Fields { get; init; }
        = new Dictionary<string, string?>();
}

public sealed class DeliveryResult
{
    public required bool Success { get; init; }
    public bool IsSkipped { get; init; }
    public string? ExternalMessageId { get; init; }
    public string? Error { get; init; }
    public bool IsTransient { get; init; }

    public static DeliveryResult Sent(string? externalId = null) => new()
    {
        Success = true,
        ExternalMessageId = externalId
    };

    public static DeliveryResult Failed(string error, bool transient = true) => new()
    {
        Success = false,
        Error = error,
        IsTransient = transient
    };

    public static DeliveryResult Skipped(string reason) => new()
    {
        Success = false,
        IsSkipped = true,
        Error = reason,
        IsTransient = false
    };
}

public sealed class SourceReadResult
{
    public required string Alias { get; init; }
    public required IReadOnlyCollection<DataRecord> Records { get; init; }
    public IReadOnlyCollection<string> Warnings { get; init; } = [];
}
