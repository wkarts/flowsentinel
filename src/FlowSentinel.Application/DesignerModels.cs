using FlowSentinel.Domain;

namespace FlowSentinel.Application;

public sealed class SourcePreviewResult
{
    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows { get; init; }
    public int TotalRead { get; init; }
    public TimeSpan Duration { get; init; }
}

public sealed class SourceConnectionTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}

public interface ISourceDesignerService
{
    Task<IReadOnlyList<string>> GetExcelWorksheetsAsync(
        string filePath,
        CancellationToken cancellationToken);

    Task<SourcePreviewResult> PreviewAsync(
        DataSourceDefinition source,
        int maximumRows,
        CancellationToken cancellationToken);

    Task<SourceConnectionTestResult> TestAsync(
        DataSourceDefinition source,
        CancellationToken cancellationToken);
}
