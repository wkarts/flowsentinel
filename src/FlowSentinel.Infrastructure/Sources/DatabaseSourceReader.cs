using System.Text.Json;
using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace FlowSentinel.Infrastructure.Sources;

internal sealed partial class DatabaseSourceReader : IDataSourceReader
{
    private readonly ISecretProtector _secretProtector;

    public DatabaseSourceReader(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
    }

    public SourceType SourceType => SourceType.Database;

    public async Task<SourceReadResult> ReadAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken)
    {
        var settings = source.Configuration.Deserialize<DatabaseSourceSettings>(FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração de banco inválida.");
        ValidateReadOnlyQuery(settings.Query);

        var connectionString = _secretProtector.UnprotectIfNeeded(settings.ConnectionString);
        await using var connection = CreateConnection(settings.Provider, connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = settings.Query;
        command.CommandTimeout = Math.Clamp(settings.CommandTimeoutSeconds, 5, 3600);

        foreach (var item in settings.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = item.Key;
            parameter.Value = (object?)item.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        var records = new List<DataRecord>();
        var rowNumber = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rowNumber++;
            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                fields[reader.GetName(index)] = reader.IsDBNull(index)
                    ? null
                    : NormalizeValue(reader.GetValue(index));
            }

            fields["__rowNumber"] = rowNumber.ToString(CultureInfo.InvariantCulture);
            var key = SourceReaderHelpers.BuildKey(fields, source.KeyFields);
            records.Add(new DataRecord
            {
                Key = key,
                SourceAlias = source.Alias,
                Fields = fields,
                CollectedAt = DateTimeOffset.Now,
                Fingerprint = SourceReaderHelpers.ComputeFingerprint(fields)
            });
        }

        return new SourceReadResult { Alias = source.Alias, Records = records };
    }

    private static DbConnection CreateConnection(DatabaseProvider provider, string connectionString) => provider switch
    {
        DatabaseProvider.Sqlite => new SqliteConnection(connectionString),
        DatabaseProvider.SqlServer => new SqlConnection(connectionString),
        DatabaseProvider.MySql => new MySqlConnection(connectionString),
        DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
        DatabaseProvider.Firebird => new FbConnection(connectionString),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Provedor não suportado.")
    };

    private static void ValidateReadOnlyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || !ReadOnlyStartRegex().IsMatch(query))
        {
            throw new InvalidOperationException("Somente consultas SELECT ou CTE iniciadas por WITH são permitidas.");
        }

        if (DestructiveKeywordRegex().IsMatch(query))
        {
            throw new InvalidOperationException("A consulta contém comandos potencialmente destrutivos.");
        }
    }

    private static string NormalizeValue(object value) => value switch
    {
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    [GeneratedRegex(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReadOnlyStartRegex();

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE|CREATE|REPLACE|MERGE|EXECUTE|EXEC|CALL|GRANT|REVOKE)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DestructiveKeywordRegex();

    private sealed class DatabaseSourceSettings
    {
        public DatabaseProvider Provider { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public int CommandTimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string?> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
