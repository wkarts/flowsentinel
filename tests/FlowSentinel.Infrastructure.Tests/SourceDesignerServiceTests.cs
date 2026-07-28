using System.Text.Json;
using FlowSentinel.Application;
using FlowSentinel.Domain;
using FlowSentinel.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FlowSentinel.Infrastructure.Tests;

public sealed class SourceDesignerServiceTests
{
    [Fact]
    public async Task PreviewCsvDeveRetornarColunasELinhasParaEditorVisual()
    {
        var root = Path.Combine(Path.GetTempPath(), $"flowsentinel-designer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var csvPath = Path.Combine(root, "clientes.csv");
        await File.WriteAllTextAsync(csvPath, "Id;Cliente;Status\n1;Empresa A;Pendente\n2;Empresa B;Pago\n");

        try
        {
            await using var provider = new ServiceCollection()
                .AddFlowSentinelInfrastructure(new AppPaths { RootDirectory = root })
                .BuildServiceProvider();
            var service = provider.GetRequiredService<ISourceDesignerService>();

            using var configuration = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                filePath = csvPath,
                delimiter = ";",
                quote = "\"",
                encoding = "utf-8",
                hasHeader = true,
                ignoreEmptyLines = true
            }, FlowJson.Options));

            var source = new DataSourceDefinition
            {
                Name = "Clientes",
                Alias = "primary",
                Type = SourceType.Csv,
                IsPrimary = true,
                KeyFields = ["Id"],
                Configuration = configuration.RootElement.Clone()
            };

            var preview = await service.PreviewAsync(source, 10, CancellationToken.None);

            Assert.Equal(2, preview.TotalRead);
            Assert.Contains("Cliente", preview.Columns);
            Assert.Equal("Empresa A", preview.Rows[0]["Cliente"]);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }
}
