using FlowSentinel.Application;
using FlowSentinel.Domain;
using FlowSentinel.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FlowSentinel.Infrastructure.Tests;

public sealed class AutomationExecutorPendingTests
{
    [Fact]
    public async Task DeveRepetirEnquantoPendentePararAoConcluirEReabrirNovoEpisodio()
    {
        var root = Path.Combine(Path.GetTempPath(), "FlowSentinel.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var reader = new MutableReader("P");

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFlowSentinelInfrastructure(new AppPaths { RootDirectory = root });
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            var store = provider.GetRequiredService<IFlowStore>();
            await store.InitializeAsync(CancellationToken.None);

            var definition = CreateAutomation();
            await store.SaveAutomationAsync(definition, CancellationToken.None);
            var executor = new AutomationExecutor(
                store,
                [reader],
                new RuleEngine(),
                new TemplateRenderer(),
                new LocalRecipientResolver(),
                NullLogger<AutomationExecutor>.Instance);

            await executor.ExecuteAsync(definition.Id, CancellationToken.None);
            var first = Assert.Single(await store.ClaimDueDeliveriesAsync(
                DateTimeOffset.Now.AddMinutes(1), 10, CancellationToken.None));
            Assert.Equal(1, first.ExecutionNumber);
            await store.CompleteDeliveryAsync(first.Id, DeliveryResult.Sent("first"), null, CancellationToken.None);

            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE action_runtime_states SET LastScheduledAt = $scheduledAt WHERE OccurrenceId = $occurrenceId AND ActionId = $actionId;";
                command.Parameters.AddWithValue("$scheduledAt", DateTime.UtcNow.AddMinutes(-1));
                command.Parameters.AddWithValue("$occurrenceId", first.OccurrenceId.ToString().ToUpperInvariant());
                command.Parameters.AddWithValue("$actionId", first.ActionId.ToString().ToUpperInvariant());
                Assert.Equal(1, await command.ExecuteNonQueryAsync());
            }
            await executor.ExecuteAsync(definition.Id, CancellationToken.None);

            reader.Status = "X";
            await executor.ExecuteAsync(definition.Id, CancellationToken.None);
            var afterCompletion = await store.ClaimDueDeliveriesAsync(
                DateTimeOffset.Now.AddMinutes(1), 10, CancellationToken.None);
            Assert.Empty(afterCompletion);

            reader.Status = "P";
            await executor.ExecuteAsync(definition.Id, CancellationToken.None);
            var reopened = Assert.Single(await store.ClaimDueDeliveriesAsync(
                DateTimeOffset.Now.AddMinutes(1), 10, CancellationToken.None));
            Assert.Equal(1, reopened.ExecutionNumber);
            Assert.NotEqual(first.IdempotencyKey, reopened.IdempotencyKey);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            DeleteDirectoryWithRetry(root);
        }
    }


    [Fact]
    public async Task NaoDevePersistirEstadoDeAcaoDeMudancaEnquantoRegistroPermanecerInalterado()
    {
        var root = Path.Combine(Path.GetTempPath(), "FlowSentinel.Tests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var reader = new MutableReader("P");

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddFlowSentinelInfrastructure(new AppPaths { RootDirectory = root });
            await using var provider = services.BuildServiceProvider(validateScopes: true);
            var store = provider.GetRequiredService<IFlowStore>();
            await store.InitializeAsync(CancellationToken.None);

            var definition = CreateChangeAutomation();
            await store.SaveAutomationAsync(definition, CancellationToken.None);
            var executor = new AutomationExecutor(
                store,
                [reader],
                new RuleEngine(),
                new TemplateRenderer(),
                new LocalRecipientResolver(),
                NullLogger<AutomationExecutor>.Instance);

            await executor.ExecuteAsync(definition.Id, CancellationToken.None);
            await executor.ExecuteAsync(definition.Id, CancellationToken.None);

            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM action_runtime_states;";
                Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }

            reader.Status = "X";
            await executor.ExecuteAsync(definition.Id, CancellationToken.None);

            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM action_runtime_states;";
                Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            }

            var delivery = Assert.Single(await store.ClaimDueDeliveriesAsync(
                DateTimeOffset.Now.AddMinutes(1), 10, CancellationToken.None));
            Assert.Equal(1, delivery.ExecutionNumber);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            DeleteDirectoryWithRetry(root);
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(attempt * 100);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(attempt * 100);
            }
        }
    }


    private static AutomationDefinition CreateChangeAutomation() => new()
    {
        Name = "Mudança sem estado redundante",
        IntervalSeconds = 5,
        Sources =
        [
            new DataSourceDefinition
            {
                Alias = "planilha",
                Name = "Planilha",
                Type = SourceType.Csv,
                IsPrimary = true,
                KeyFields = ["Id"]
            }
        ],
        EntryRules = RuleSetDefinition.AlwaysTrue(RuleSetType.Entry),
        Actions =
        [
            new ActionDefinition
            {
                Name = "Mudança de situação",
                Trigger = ActionTrigger.WhileActive,
                EvaluateWhileActiveOnOpen = false,
                Repeat = new RepeatPolicyDefinition
                {
                    Enabled = true,
                    IntervalSeconds = 1,
                    MaxExecutions = 0,
                    ResetOnConditionReentry = false
                },
                Conditions = new RuleSetDefinition
                {
                    Type = RuleSetType.ActionCondition,
                    Root = new RuleGroupDefinition
                    {
                        Rules =
                        [
                            new RuleDefinition
                            {
                                Field = "Status",
                                Operator = RuleOperator.Changed
                            }
                        ]
                    }
                },
                SubjectTemplate = "Mudança {{Entity}}",
                MessageTemplate = "{{Entity}} foi alterado.",
                Channels =
                [
                    new ActionChannelDefinition
                    {
                        ChannelConfigurationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        ChannelType = ChannelType.LocalWindows,
                        GroupingMode = NotificationGroupingMode.Individual,
                        GroupingWindowSeconds = 0
                    }
                ]
            }
        ]
    };

    private static AutomationDefinition CreateAutomation() => new()
    {
        Name = "Pendência por período",
        IntervalSeconds = 5,
        Sources =
        [
            new DataSourceDefinition
            {
                Alias = "planilha",
                Name = "Planilha",
                Type = SourceType.Csv,
                IsPrimary = true,
                KeyFields = ["Id"]
            }
        ],
        EntryRules = RuleSetDefinition.AlwaysTrue(RuleSetType.Entry),
        Actions =
        [
            new ActionDefinition
            {
                Name = "Lembrete de pendência",
                Trigger = ActionTrigger.WhileActive,
                EvaluateWhileActiveOnOpen = true,
                Repeat = new RepeatPolicyDefinition
                {
                    Enabled = true,
                    IntervalSeconds = 1,
                    MaxExecutions = 0,
                    ResetOnConditionReentry = true
                },
                Conditions = Rules(RuleSetType.ActionCondition, RuleOperator.In, "P"),
                PersistenceConditions = Rules(RuleSetType.ActionPersistence, RuleOperator.NotIn, "X"),
                CompletionConditions = Rules(RuleSetType.ActionCompletion, RuleOperator.In, "X"),
                CancelPendingWhenConditionFails = true,
                SubjectTemplate = "Pendência {{Entity}}",
                MessageTemplate = "{{Entity}} permanece pendente.",
                Channels =
                [
                    new ActionChannelDefinition
                    {
                        ChannelConfigurationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        ChannelType = ChannelType.LocalWindows,
                        GroupingMode = NotificationGroupingMode.Individual,
                        GroupingWindowSeconds = 0
                    }
                ]
            }
        ]
    };

    private static RuleSetDefinition Rules(RuleSetType type, RuleOperator ruleOperator, string expectedValue) => new()
    {
        Type = type,
        Root = new RuleGroupDefinition
        {
            Rules =
            [
                new RuleDefinition
                {
                    Field = "Status",
                    Operator = ruleOperator,
                    ExpectedValue = expectedValue
                }
            ]
        }
    };

    private sealed class MutableReader(string status) : IDataSourceReader
    {
        public SourceType SourceType => SourceType.Csv;
        public string Status { get; set; } = status;

        public Task<SourceReadResult> ReadAsync(DataSourceDefinition source, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SourceReadResult
            {
                Alias = source.Alias,
                Records =
                [
                    new DataRecord
                    {
                        Key = "CLIENTE-1|JAN",
                        SourceAlias = source.Alias,
                        Fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Id"] = "1",
                            ["Entity"] = "Cliente 1",
                            ["Status"] = Status,
                            ["Period"] = "JAN"
                        }
                    }
                ]
            });
        }
    }

    private sealed class LocalRecipientResolver : IRecipientResolver
    {
        public Task<IReadOnlyCollection<ResolvedRecipient>> ResolveAsync(
            AutomationDefinition automation,
            ActionDefinition action,
            ChannelType channelType,
            EvaluationContext context,
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<ResolvedRecipient> recipients =
            [
                new ResolvedRecipient
                {
                    ChannelType = channelType,
                    Address = "local",
                    DisplayName = "Windows"
                }
            ];
            return Task.FromResult(recipients);
        }
    }
}
