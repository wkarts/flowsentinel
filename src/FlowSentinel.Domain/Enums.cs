namespace FlowSentinel.Domain;

public enum SourceType
{
    Excel,
    Csv,
    Text,
    Database
}

public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
    MySql,
    PostgreSql,
    Firebird
}

public enum LogicalOperator
{
    And,
    Or
}

public enum RuleSetType
{
    Entry,
    Persistence,
    Completion,
    Suspension,
    Reopening,
    ActionCondition
}

public enum RuleOperator
{
    Equal,
    NotEqual,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    IsEmpty,
    IsNotEmpty,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    In,
    NotIn,
    Regex,
    Exists,
    NotExists,
    Changed,
    Unchanged,
    ChangedFromTo
}

public enum OccurrenceStatus
{
    New,
    Active,
    Suspended,
    Resolved,
    Cancelled,
    Failed
}

public enum DeliveryStatus
{
    Pending,
    Processing,
    Sent,
    RetryScheduled,
    Failed,
    Cancelled,
    Skipped
}

public enum ChannelType
{
    LocalWindows,
    EvolutionApi,
    Telegram,
    Email
}

public enum RecipientType
{
    Fixed,
    Field,
    Contact,
    Group
}

public enum ActionTrigger
{
    OnOpen,
    WhileActive,
    OnResolved
}

public enum MissingRecordBehavior
{
    Ignore,
    Resolve
}

public enum ChannelExecutionStrategy
{
    All,
    AtLeastOne,
    FirstSuccessful
}

public enum NotificationGroupingMode
{
    Individual,
    ByEntity,
    SingleMessage
}

public enum ContactAccessScope
{
    AllAutomations,
    SelectedAutomations
}

public enum ActionSuccessPolicy
{
    AllDeliveries,
    AtLeastOneDelivery
}
