using FlowSentinel.Application;
using FlowSentinel.Domain;
using FlowSentinel.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace FlowSentinel.Infrastructure.Tests;

public sealed class FlowStoreSqliteTests
{
    [Fact]
    public async Task DashboardDeveConsultarUltimaExecucaoSemFalharNoSQLite()
    {
        var root = CreateTemporaryRoot();

        try
        {
            await using var provider = CreateProvider(root);
            var store = provider.GetRequiredService<IFlowStore>();

            await store.InitializeAsync(CancellationToken.None);

            var emptySnapshot = await store.GetDashboardSnapshotAsync(CancellationToken.None);
            Assert.Null(emptySnapshot.LastExecutionAt);

            var definition = CreateAutomation("Dashboard");
            await store.SaveAutomationAsync(definition, CancellationToken.None);
            await store.MarkAutomationExecutionAsync(
                definition.Id,
                DateTimeOffset.UtcNow.AddMinutes(5),
                null,
                CancellationToken.None);

            var snapshot = await store.GetDashboardSnapshotAsync(CancellationToken.None);

            Assert.True(snapshot.EnabledAutomations >= 1);
            Assert.NotNull(snapshot.LastExecutionAt);
            Assert.Equal(TimeSpan.Zero, snapshot.LastExecutionAt.Value.Offset);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task InicializacaoDeveNormalizarDatasDoBancoLegadoSemExcluirDados()
    {
        var root = CreateTemporaryRoot();
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var definition = CreateAutomation("Legado");

        try
        {
            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                await store.SaveAutomationAsync(definition, CancellationToken.None);
                await store.MarkAutomationExecutionAsync(
                    definition.Id,
                    DateTimeOffset.UtcNow.AddMinutes(5),
                    null,
                    CancellationToken.None);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE automations
                    SET LastRunAt = '2026-07-28 04:29:34-03:00',
                        NextRunAt = '2026-07-28 04:30:34-03:00',
                        CreatedAt = '2026-07-28 04:20:00-03:00',
                        UpdatedAt = '2026-07-28 04:29:34-03:00';
                    """;
                await command.ExecuteNonQueryAsync();

                command.CommandText = "PRAGMA user_version = 1;";
                await command.ExecuteNonQueryAsync();
            }

            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);

                var snapshot = await store.GetDashboardSnapshotAsync(CancellationToken.None);
                var automation = Assert.Single(
                    await store.GetAutomationsAsync(CancellationToken.None),
                    item => item.Id == definition.Id);

                Assert.Equal(
                    new DateTimeOffset(2026, 7, 28, 7, 29, 34, TimeSpan.Zero),
                    snapshot.LastExecutionAt);
                Assert.Equal(
                    new DateTimeOffset(2026, 7, 28, 7, 30, 34, TimeSpan.Zero),
                    automation.NextRunAt);
            }

            await using var validationConnection = new SqliteConnection(
                $"Data Source={databasePath};Pooling=False");
            await validationConnection.OpenAsync();
            await using var validationCommand = validationConnection.CreateCommand();
            validationCommand.CommandText = "PRAGMA user_version;";
            Assert.Equal(4L, Convert.ToInt64(await validationCommand.ExecuteScalarAsync()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTemporaryRoot(root);
        }
    }


    [Fact]
    public async Task AtualizacaoDeveDesativarAgregadosLegadosDaRp102ECancelarEntregasPendentes()
    {
        var root = CreateTemporaryRoot();
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var actionId = Guid.NewGuid();
        var definition = CreateLegacyRp102Automation(actionId);
        var occurrenceId = Guid.NewGuid();

        try
        {
            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                await store.SaveAutomationAsync(definition, CancellationToken.None);
                await store.CreateOccurrenceAsync(new OccurrenceStoreItem
                {
                    Id = occurrenceId,
                    AutomationId = definition.Id,
                    RecordKey = "Aggregate|Global|Todos|JAN|X",
                    Status = OccurrenceStatus.Active,
                    OpenedAt = DateTimeOffset.UtcNow,
                    LastEvaluatedAt = DateTimeOffset.UtcNow,
                    Snapshot = new Dictionary<string, string?>(),
                    Fingerprint = "legacy"
                }, CancellationToken.None);
                await store.AddDeliveriesAsync(
                    [CreateDelivery(definition.Id, occurrenceId, actionId, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)],
                    CancellationToken.None);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version = 2;";
                await command.ExecuteNonQueryAsync();
            }

            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                var upgraded = await store.GetAutomationDefinitionAsync(definition.Id, CancellationToken.None);
                Assert.NotNull(upgraded);
                var action = Assert.Single(upgraded.Actions, x => x.Id == actionId);
                Assert.False(action.Enabled);

                var matrix = upgraded.Sources.Single().Configuration.GetProperty("matrix");
                Assert.False(matrix.GetProperty("generateAggregateRecords").GetBoolean());
                Assert.False(matrix.GetProperty("aggregateGlobal").GetBoolean());
                Assert.False(matrix.GetProperty("aggregateBySection").GetBoolean());
                Assert.False(matrix.GetProperty("aggregateByCollaborator").GetBoolean());
                Assert.False(matrix.GetProperty("includeBlankValuesInAggregates").GetBoolean());

                var due = await store.ClaimDueDeliveriesAsync(DateTimeOffset.UtcNow.AddMinutes(1), 10, CancellationToken.None);
                Assert.DoesNotContain(due, x => x.ActionId == actionId);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task AtualizacaoNaoDeveDesativarAgregadosDeMatrizGenerica()
    {
        var root = CreateTemporaryRoot();
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var actionId = Guid.NewGuid();
        var definition = CreateGenericMatrixAutomation(actionId);

        try
        {
            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                await store.SaveAutomationAsync(definition, CancellationToken.None);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version = 3;";
                await command.ExecuteNonQueryAsync();
            }

            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                var upgraded = await store.GetAutomationDefinitionAsync(definition.Id, CancellationToken.None);
                Assert.NotNull(upgraded);
                Assert.True(Assert.Single(upgraded.Actions, x => x.Id == actionId).Enabled);
                Assert.True(upgraded.Sources.Single().Configuration
                    .GetProperty("matrix")
                    .GetProperty("generateAggregateRecords")
                    .GetBoolean());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task AtualizacaoDeveIgnorarEntregasDeCanalDesabilitadoSemRegistrarFalha()
    {
        var root = CreateTemporaryRoot();
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var channelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var definition = CreateAutomation("Canal desabilitado");
        var occurrenceId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var delivery = CreateDelivery(
            definition.Id,
            occurrenceId,
            actionId,
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        try
        {
            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                await store.SaveAutomationAsync(definition, CancellationToken.None);
                await store.SaveChannelConfigurationAsync(new ChannelConfiguration
                {
                    Id = channelId,
                    Name = "Notificação local desabilitada",
                    Type = ChannelType.LocalWindows,
                    Enabled = false
                }, CancellationToken.None);
                await store.CreateOccurrenceAsync(new OccurrenceStoreItem
                {
                    Id = occurrenceId,
                    AutomationId = definition.Id,
                    RecordKey = "CLIENTE-SEM-CANAL",
                    Status = OccurrenceStatus.Active,
                    OpenedAt = DateTimeOffset.UtcNow,
                    LastEvaluatedAt = DateTimeOffset.UtcNow,
                    Snapshot = new Dictionary<string, string?>(),
                    Fingerprint = "disabled-channel"
                }, CancellationToken.None);
                await store.AddDeliveriesAsync([delivery], CancellationToken.None);
            }

            await using (var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE deliveries SET Status = $status, LastError = 'Configuração de canal inexistente ou desabilitada.' WHERE Id = $id;";
                command.Parameters.AddWithValue("$status", (int)DeliveryStatus.Failed);
                command.Parameters.AddWithValue("$id", delivery.Id.ToString().ToUpperInvariant());
                await command.ExecuteNonQueryAsync();

                command.Parameters.Clear();
                command.CommandText = "PRAGMA user_version = 3;";
                await command.ExecuteNonQueryAsync();
            }

            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                var snapshot = await store.GetDashboardSnapshotAsync(CancellationToken.None);
                Assert.Equal(0, snapshot.FailedDeliveries);
            }

            await using var validationConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await validationConnection.OpenAsync();
            await using var validationCommand = validationConnection.CreateCommand();
            validationCommand.CommandText = "SELECT Status, LastError FROM deliveries WHERE Id = $id;";
            validationCommand.Parameters.AddWithValue("$id", delivery.Id.ToString().ToUpperInvariant());
            await using var reader = await validationCommand.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal((int)DeliveryStatus.Skipped, reader.GetInt32(0));
            Assert.Contains("Canal removido ou desabilitado", reader.GetString(1), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task ConclusaoDeEntregaIgnoradaDeveUsarStatusSkipped()
    {
        var root = CreateTemporaryRoot();
        var databasePath = Path.Combine(root, "data", "flowsentinel.db");
        var definition = CreateAutomation("Entrega ignorada");
        var occurrenceId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var delivery = CreateDelivery(
            definition.Id,
            occurrenceId,
            actionId,
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        try
        {
            await using (var provider = CreateProvider(root))
            {
                var store = provider.GetRequiredService<IFlowStore>();
                await store.InitializeAsync(CancellationToken.None);
                await store.SaveAutomationAsync(definition, CancellationToken.None);
                await store.CreateOccurrenceAsync(new OccurrenceStoreItem
                {
                    Id = occurrenceId,
                    AutomationId = definition.Id,
                    RecordKey = "IGNORADO",
                    Status = OccurrenceStatus.Active,
                    OpenedAt = DateTimeOffset.UtcNow,
                    LastEvaluatedAt = DateTimeOffset.UtcNow,
                    Snapshot = new Dictionary<string, string?>(),
                    Fingerprint = "skipped"
                }, CancellationToken.None);
                await store.AddDeliveriesAsync([delivery], CancellationToken.None);
                await store.CompleteDeliveryAsync(
                    delivery.Id,
                    DeliveryResult.Skipped("Canal desabilitado para esta entrega."),
                    null,
                    CancellationToken.None);
            }

            await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Status, SentAt, LastError FROM deliveries WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", delivery.Id.ToString().ToUpperInvariant());
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal((int)DeliveryStatus.Skipped, reader.GetInt32(0));
            Assert.True(reader.IsDBNull(1));
            Assert.Equal("Canal desabilitado para esta entrega.", reader.GetString(2));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task AgendamentoDeveCompararOrdenarEAgruparDatasSemDateTimeOffsetNoSQLite()
    {
        var root = CreateTemporaryRoot();

        try
        {
            await using var provider = CreateProvider(root);
            var store = provider.GetRequiredService<IFlowStore>();
            await store.InitializeAsync(CancellationToken.None);

            var definition = CreateAutomation("Agendamento");
            await store.SaveAutomationAsync(definition, CancellationToken.None);

            var due = await store.GetDueAutomationsAsync(
                DateTimeOffset.UtcNow.AddMinutes(1),
                CancellationToken.None);
            Assert.Contains(due, item => item.Id == definition.Id);

            var occurrenceId = Guid.NewGuid();
            var actionId = Guid.NewGuid();
            var firstCreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            var secondCreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

            await store.CreateOccurrenceAsync(
                new OccurrenceStoreItem
                {
                    Id = occurrenceId,
                    AutomationId = definition.Id,
                    RecordKey = "CLIENTE-1",
                    Status = OccurrenceStatus.Active,
                    OpenedAt = firstCreatedAt,
                    LastEvaluatedAt = secondCreatedAt,
                    Snapshot = new Dictionary<string, string?>(),
                    Fingerprint = "TESTE"
                },
                CancellationToken.None);

            await store.AddDeliveriesAsync(
                [
                    CreateDelivery(definition.Id, occurrenceId, actionId, 1, firstCreatedAt, firstCreatedAt),
                    CreateDelivery(definition.Id, occurrenceId, actionId, 2, secondCreatedAt, secondCreatedAt),
                    CreateDelivery(
                        definition.Id,
                        occurrenceId,
                        actionId,
                        3,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.AddHours(1))
                ],
                CancellationToken.None);

            var schedule = await store.GetActionScheduleStateAsync(
                occurrenceId,
                actionId,
                CancellationToken.None);

            Assert.Equal(3, schedule.ExecutionCount);
            Assert.NotNull(schedule.LastScheduledAt);
            Assert.Equal(TimeSpan.Zero, schedule.LastScheduledAt.Value.Offset);

            var claimed = await store.ClaimDueDeliveriesAsync(
                DateTimeOffset.UtcNow,
                10,
                CancellationToken.None);

            Assert.Equal(2, claimed.Count);
            Assert.All(claimed, delivery => Assert.True(delivery.DueAt <= DateTimeOffset.UtcNow));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static ServiceProvider CreateProvider(string root)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFlowSentinelInfrastructure(new AppPaths { RootDirectory = root });
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static AutomationDefinition CreateAutomation(string suffix) => new()
    {
        Name = $"Automação SQLite {suffix}",
        Enabled = true,
        IntervalSeconds = 60,
        Sources =
        [
            new DataSourceDefinition
            {
                Alias = "principal",
                Name = "Fonte principal",
                Type = SourceType.Csv,
                IsPrimary = true,
                KeyFields = ["Id"]
            }
        ]
    };


    private static AutomationDefinition CreateLegacyRp102Automation(Guid actionId)
    {
        using var document = System.Text.Json.JsonDocument.Parse("""
        {
          "filePath": "C:\\Dados\\RP-102.xlsx",
          "mode": "SectionedMatrix",
          "profileName": "Matriz contábil RP-102",
          "matrix": {
            "generateAggregateRecords": true,
            "aggregateGlobal": true,
            "aggregateBySection": true,
            "aggregateByCollaborator": true,
            "includeBlankValuesInAggregates": true
          }
        }
        """);

        return new AutomationDefinition
        {
            Name = "RP-102 legada",
            Enabled = true,
            IntervalSeconds = 60,
            Sources =
            [
                new DataSourceDefinition
                {
                    Alias = "planilha",
                    Name = "RP-102",
                    Type = SourceType.Excel,
                    IsPrimary = true,
                    KeyFields = ["__recordKey"],
                    Configuration = document.RootElement.Clone()
                }
            ],
            Actions =
            [
                new ActionDefinition
                {
                    Id = actionId,
                    Name = "Mudança de quantidade por situação",
                    Enabled = true,
                    Trigger = ActionTrigger.WhileActive,
                    MessageTemplate = "O indicador {{Metric}} mudou.",
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
    }

    private static AutomationDefinition CreateGenericMatrixAutomation(Guid actionId)
    {
        using var document = System.Text.Json.JsonDocument.Parse("""
        {
          "filePath": "C:\\Dados\\matriz-generica.xlsx",
          "mode": "SectionedMatrix",
          "profileName": "Matriz de equipamentos",
          "matrix": {
            "generateAggregateRecords": true,
            "aggregateGlobal": true,
            "aggregateBySection": true,
            "aggregateByCollaborator": true,
            "includeBlankValuesInAggregates": false
          }
        }
        """);

        return new AutomationDefinition
        {
            Name = "Monitoramento de equipamentos",
            Enabled = true,
            IntervalSeconds = 60,
            Sources =
            [
                new DataSourceDefinition
                {
                    Alias = "planilha",
                    Name = "Matriz de equipamentos",
                    Type = SourceType.Excel,
                    IsPrimary = true,
                    KeyFields = ["__recordKey"],
                    Configuration = document.RootElement.Clone()
                }
            ],
            Actions =
            [
                new ActionDefinition
                {
                    Id = actionId,
                    Name = "Mudança de quantidade por situação",
                    Enabled = true,
                    Trigger = ActionTrigger.WhileActive,
                    MessageTemplate = "O indicador {{Metric}} mudou.",
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
    }

    private static DeliveryStoreItem CreateDelivery(
        Guid automationId,
        Guid occurrenceId,
        Guid actionId,
        int executionNumber,
        DateTimeOffset createdAt,
        DateTimeOffset dueAt) => new()
    {
        Id = Guid.NewGuid(),
        OccurrenceId = occurrenceId,
        AutomationId = automationId,
        ActionId = actionId,
        ChannelConfigurationId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ChannelType = ChannelType.LocalWindows,
        Recipient = "local",
        Subject = "Teste",
        Message = "Teste SQLite",
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        ExecutionNumber = executionNumber,
        CreatedAt = createdAt,
        Status = DeliveryStatus.Pending,
        AttemptCount = 0,
        DueAt = dueAt,
        Fields = new Dictionary<string, string?>()
    };

    private static string CreateTemporaryRoot() => Path.Combine(
        Path.GetTempPath(),
        "FlowSentinel.Tests",
        Guid.NewGuid().ToString("N"));

    private static void DeleteTemporaryRoot(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
