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
            for (var column = 1; column <= lastColumn; column++)
            {
                var header = headerRow.Cell(column).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(header))
                {
                    headers[column] = header;
                }
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
        finally
        {
            try { File.Delete(snapshot); } catch { }
        }
    }

    private sealed class ExcelSourceSettings
    {
        public string FilePath { get; set; } = string.Empty;
        public string? Worksheet { get; set; }
        public int HeaderRow { get; set; } = 1;
        public bool IgnoreEmptyRows { get; set; } = true;
    }
}
