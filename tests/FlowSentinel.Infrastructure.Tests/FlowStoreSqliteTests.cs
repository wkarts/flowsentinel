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
            Assert.Equal(2L, Convert.ToInt64(await validationCommand.ExecuteScalarAsync()));
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
