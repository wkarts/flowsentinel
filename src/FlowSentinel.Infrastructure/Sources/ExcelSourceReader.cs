using System.Text.Json;
using ClosedXML.Excel;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal sealed class ExcelSourceReader : IDataSourceReader
{
    public SourceType SourceType => SourceType.Excel;

    public async Task<SourceReadResult> ReadAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken)
    {
        var settings = source.Configuration.Deserialize<ExcelSourceSettings>(FlowJson.Options)
                       ?? throw new InvalidOperationException("Configuração de Excel inválida.");
        var path = SourceReaderHelpers.ResolvePath(settings.FilePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("A planilha configurada não foi encontrada.", path);
        }

        var snapshot = await SourceReaderHelpers.CreateSnapshotAsync(path, cancellationToken);
        try
        {
            using var workbook = new XLWorkbook(snapshot);
            if (settings.Mode.Equals("SectionedMatrix", StringComparison.OrdinalIgnoreCase))
            {
                return ReadSectionedMatrix(source, workbook, settings, cancellationToken);
            }

            return ReadFlatTable(source, workbook, settings, cancellationToken);
        }
        finally
        {
            try { File.Delete(snapshot); } catch { }
        }
    }

    private static SourceReadResult ReadSectionedMatrix(
        DataSourceDefinition source,
        XLWorkbook workbook,
        ExcelSourceSettings settings,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var worksheets = ExcelSectionedMatrixParser.ResolveWorksheets(workbook, settings);
        var records = new List<DataRecord>();
        foreach (var worksheet in worksheets)
        {
            records.AddRange(ExcelSectionedMatrixParser.Parse(
                source.Alias,
                worksheet,
                settings,
                warnings,
                cancellationToken));
        }

        var duplicateKeys = records
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .Take(20)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            throw new InvalidOperationException(
                $"A leitura da matriz produziu chaves duplicadas. Exemplos: {string.Join(", ", duplicateKeys)}");
        }

        return new SourceReadResult
        {
            Alias = source.Alias,
            Records = records,
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    private static SourceReadResult ReadFlatTable(
        DataSourceDefinition source,
        XLWorkbook workbook,
        ExcelSourceSettings settings,
        CancellationToken cancellationToken)
    {
        var worksheet = string.IsNullOrWhiteSpace(settings.Worksheet)
            ? workbook.Worksheets.First()
            : workbook.Worksheet(settings.Worksheet);

        var headerRowNumber = Math.Max(1, settings.HeaderRow);
        var headerRow = worksheet.Row(headerRowNumber);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;
        if (lastColumn == 0 || lastRow <= headerRowNumber)
        {
            return new SourceReadResult { Alias = source.Alias, Records = [] };
        }

        var headers = new Dictionary<int, string>();
        var headerOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            var header = headerRow.Cell(column).GetString().Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            headerOccurrences[header] = headerOccurrences.GetValueOrDefault(header) + 1;
            var occurrence = headerOccurrences[header];
            var normalized = occurrence > 1 ? $"{header}_{occurrence}" : header;
            headers[column] = normalized;
        }

        var records = new List<DataRecord>();
        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
            {
                var value = row.Cell(header.Key).GetFormattedString().Trim();
                fields[header.Value] = string.IsNullOrEmpty(value) ? null : value;
            }

            if (settings.IgnoreEmptyRows && fields.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            fields["__rowNumber"] = rowNumber.ToString();
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
}
