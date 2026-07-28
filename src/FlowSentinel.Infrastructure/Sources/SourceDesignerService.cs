using System.Diagnostics;
using ClosedXML.Excel;
using FlowSentinel.Application;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal sealed class SourceDesignerService : ISourceDesignerService
{
    private readonly IReadOnlyDictionary<SourceType, IDataSourceReader> _readers;

    public SourceDesignerService(IEnumerable<IDataSourceReader> readers)
    {
        _readers = readers.ToDictionary(x => x.SourceType);
    }

    public async Task<IReadOnlyList<string>> GetExcelWorksheetsAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return [];
        }

        var path = SourceReaderHelpers.ResolvePath(filePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("A planilha informada não foi encontrada.", path);
        }

        var snapshot = await SourceReaderHelpers.CreateSnapshotAsync(path, cancellationToken);
        try
        {
            using var workbook = new XLWorkbook(snapshot);
            return workbook.Worksheets.Select(x => x.Name).ToArray();
        }
        finally
        {
            try { File.Delete(snapshot); } catch { }
        }
    }

    public async Task<SourcePreviewResult> PreviewAsync(
        DataSourceDefinition source,
        int maximumRows,
        CancellationToken cancellationToken)
    {
        if (!_readers.TryGetValue(source.Type, out var reader))
        {
            throw new InvalidOperationException($"Não existe leitor registrado para a fonte {source.Type}.");
        }

        var previewSource = CloneForPreview(source);
        var stopwatch = Stopwatch.StartNew();
        var result = await reader.ReadAsync(previewSource, cancellationToken);
        stopwatch.Stop();

        var selectedRows = result.Records.Take(Math.Clamp(maximumRows, 1, 500)).ToArray();
        var columns = selectedRows
            .SelectMany(x => x.Fields.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.StartsWith("__", StringComparison.Ordinal) ? 1 : 0)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SourcePreviewResult
        {
            Columns = columns,
            Rows = selectedRows.Select(x => (IReadOnlyDictionary<string, string?>)x.Fields).ToArray(),
            TotalRead = result.Records.Count,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<SourceConnectionTestResult> TestAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var preview = await PreviewAsync(source, 1, cancellationToken);
            stopwatch.Stop();
            return new SourceConnectionTestResult
            {
                Success = true,
                Message = $"Fonte acessada com sucesso. {preview.TotalRead} registro(s) localizado(s).",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new SourceConnectionTestResult
            {
                Success = false,
                Message = exception.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private static DataSourceDefinition CloneForPreview(DataSourceDefinition source)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source, FlowJson.Options);
        var clone = System.Text.Json.JsonSerializer.Deserialize<DataSourceDefinition>(json, FlowJson.Options)
                    ?? throw new InvalidOperationException("Não foi possível preparar a pré-visualização da fonte.");

        if (clone.KeyFields.Count == 0)
        {
            clone.KeyFields = clone.Type switch
            {
                SourceType.Excel => ["__rowNumber"],
                SourceType.Csv => ["__lineNumber"],
                SourceType.Text => ["LineNumber"],
                SourceType.Database => ["__rowNumber"],
                _ => ["__rowNumber"]
            };
        }

        return clone;
    }
}
