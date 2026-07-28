using System.Text.Json;
using System.Text;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal sealed class CsvSourceReader : IDataSourceReader
{
    public SourceType SourceType => SourceType.Csv;

    public async Task<SourceReadResult> ReadAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken)
    {
        var settings = source.Configuration.Deserialize<CsvSourceSettings>(FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração CSV inválida.");
        var path = SourceReaderHelpers.ResolvePath(settings.FilePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("O arquivo CSV configurado não foi encontrado.", path);
        }

        var encoding = SourceReaderHelpers.ResolveEncoding(settings.Encoding);
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);

        var headers = new List<string>();
        var records = new List<DataRecord>();
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (settings.IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseLine(line, settings.Delimiter, settings.Quote);
            if (headers.Count == 0)
            {
                if (settings.HasHeader)
                {
                    headers.AddRange(values.Select((x, i) => string.IsNullOrWhiteSpace(x) ? $"Column{i + 1}" : x.Trim()));
                    continue;
                }

                headers.AddRange(values.Select((_, i) => $"Column{i + 1}"));
            }

            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Count; index++)
            {
                var value = index < values.Count ? values[index].Trim() : null;
                fields[headers[index]] = string.IsNullOrEmpty(value) ? null : value;
            }
            fields["__lineNumber"] = lineNumber.ToString();

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

    private static IReadOnlyList<string> ParseLine(string line, string delimiter, string quote)
    {
        if (string.IsNullOrEmpty(delimiter))
        {
            throw new InvalidOperationException("O delimitador CSV não pode ser vazio.");
        }

        var delimiterChar = delimiter[0];
        var quoteChar = string.IsNullOrEmpty(quote) ? '"' : quote[0];
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == quoteChar)
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == quoteChar)
                {
                    current.Append(quoteChar);
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
                continue;
            }

            if (character == delimiterChar && !quoted)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString());
        return values;
    }

    private sealed class CsvSourceSettings
    {
        public string FilePath { get; set; } = string.Empty;
        public string Delimiter { get; set; } = ";";
        public string Quote { get; set; } = "\"";
        public string Encoding { get; set; } = "utf-8";
        public bool HasHeader { get; set; } = true;
        public bool IgnoreEmptyLines { get; set; } = true;
    }
}
