using System.Text.Json;
using System.Text.RegularExpressions;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal sealed class TextSourceReader : IDataSourceReader
{
    public SourceType SourceType => SourceType.Text;

    public async Task<SourceReadResult> ReadAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken)
    {
        var settings = source.Configuration.Deserialize<TextSourceSettings>(FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração TXT inválida.");
        var path = SourceReaderHelpers.ResolvePath(settings.FilePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("O arquivo TXT configurado não foi encontrado.", path);
        }

        var encoding = SourceReaderHelpers.ResolveEncoding(settings.Encoding);
        var lines = await File.ReadAllLinesAsync(path, encoding, cancellationToken);
        return settings.Mode.Equals("KeyValue", StringComparison.OrdinalIgnoreCase)
            ? ReadKeyValue(source, settings, lines)
            : ReadLines(source, settings, lines, cancellationToken);
    }

    private static SourceReadResult ReadKeyValue(
        DataSourceDefinition source,
        TextSourceSettings settings,
        IReadOnlyCollection<string> lines)
    {
        var separator = string.IsNullOrEmpty(settings.KeyValueSeparator) ? "=" : settings.KeyValueSeparator;
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (settings.IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var index = line.IndexOf(separator, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var key = line[..index].Trim();
            var value = line[(index + separator.Length)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                fields[key] = string.IsNullOrEmpty(value) ? null : value;
            }
        }

        var recordKey = SourceReaderHelpers.BuildKey(fields, source.KeyFields);
        return new SourceReadResult
        {
            Alias = source.Alias,
            Records =
            [
                new DataRecord
                {
                    Key = recordKey,
                    SourceAlias = source.Alias,
                    Fields = fields,
                    CollectedAt = DateTimeOffset.Now,
                    Fingerprint = SourceReaderHelpers.ComputeFingerprint(fields)
                }
            ]
        };
    }

    private static SourceReadResult ReadLines(
        DataSourceDefinition source,
        TextSourceSettings settings,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        Regex? regex = null;
        if (!string.IsNullOrWhiteSpace(settings.RecordRegex))
        {
            regex = new Regex(settings.RecordRegex, RegexOptions.Compiled, TimeSpan.FromSeconds(2));
        }

        var records = new List<DataRecord>();
        for (var index = 0; index < lines.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[index];
            if (settings.IgnoreEmptyLines && string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["LineNumber"] = (index + 1).ToString(),
                ["Content"] = line
            };

            if (regex is not null)
            {
                var match = regex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                foreach (var groupName in regex.GetGroupNames().Where(x => !int.TryParse(x, out _)))
                {
                    fields[groupName] = match.Groups[groupName].Success
                        ? match.Groups[groupName].Value
                        : null;
                }
            }

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

    private sealed class TextSourceSettings
    {
        public string FilePath { get; set; } = string.Empty;
        public string Encoding { get; set; } = "utf-8";
        public string Mode { get; set; } = "Lines";
        public string KeyValueSeparator { get; set; } = "=";
        public string? RecordRegex { get; set; }
        public bool IgnoreEmptyLines { get; set; } = true;
    }
}
