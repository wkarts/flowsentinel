using System.Security.Cryptography;
using System.Text;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal static class SourceReaderHelpers
{
    static SourceReaderHelpers()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static string ResolvePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    public static Encoding ResolveEncoding(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        return Encoding.GetEncoding(name);
    }

    public static string BuildKey(
        IReadOnlyDictionary<string, string?> fields,
        IReadOnlyCollection<string> keyFields)
    {
        var values = keyFields.Select(field =>
        {
            if (!fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"O campo-chave '{field}' não foi encontrado ou está vazio.");
            }
            return value.Trim();
        });

        return string.Join("|", values);
    }

    public static string ComputeFingerprint(IReadOnlyDictionary<string, string?> fields)
    {
        var normalized = string.Join("\n", fields.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{x.Key}={x.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    public static async Task<string> CreateSnapshotAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"flowsentinel-{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
        await using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var output = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await input.CopyToAsync(output, cancellationToken);
        return tempPath;
    }
}
