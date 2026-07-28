using ClosedXML.Excel;
using FlowSentinel.Infrastructure.Sources;

namespace FlowSentinel.Infrastructure.Tests;

public sealed class ExcelSectionedMatrixParserTests
{
    [Fact]
    public void DeveTransformarPlanilhaComBlocosEmEmpresasSituacoesETotais()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("CONF CONT 2026");
        worksheet.Cell("A1").Value = "Nº";
        worksheet.Cell("B1").Value = "EMPRESAS LUCRO REAL";
        worksheet.Cell("C1").Value = "Código";
        worksheet.Cell("D1").Value = "Colaborador";
        worksheet.Cell("E1").Value = "JAN";
        worksheet.Cell("F1").Value = "FEV";
        worksheet.Cell("G1").Value = "BAL";
        worksheet.Cell("H1").Value = "MAR";
        worksheet.Cell("I1").Value = "BAL";

        AddCompany(worksheet, 2, "1", "Empresa A", "10", "Ana", ["X", "SM", "X", "M", "X"]);
        AddCompany(worksheet, 3, "2", "Empresa B", "20", "Bruno", ["X", "X", "M", "M", "M"]);
        worksheet.Cell("E2").Style.Fill.PatternType = XLFillPatternValues.Solid;
        worksheet.Cell("E2").Style.Fill.PatternColor = XLColor.Yellow;

        worksheet.Cell("B5").Value = "SIMPLES";
        AddCompany(worksheet, 6, "1", "Empresa C", "30", "Carlos", ["SM", "SM", "X", "X", "X"]);

        var settings = new ExcelSourceSettings
        {
            Mode = "SectionedMatrix",
            WorksheetPattern = @"(?<year>20\d{2})",
            Matrix = new ExcelMatrixSettings
            {
                LastPeriodColumn = 9,
                StatusLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["X"] = "Conferido",
                    ["SM"] = "Sem movimento"
                }
            }
        };
        var warnings = new List<string>();

        var records = ExcelSectionedMatrixParser.Parse(
            "planilha",
            worksheet,
            settings,
            warnings,
            CancellationToken.None);

        var companies = records.Where(x => x.Fields.GetValueOrDefault("__recordType") == "Company").ToArray();
        Assert.Equal(3, companies.Length);
        Assert.Equal("M", companies.Single(x => x.Fields.GetValueOrDefault("Company") == "Empresa A").Fields["CurrentStatus"]);

        var statuses = records.Where(x => x.Fields.GetValueOrDefault("__recordType") == "Status").ToArray();
        Assert.Contains(statuses, x => x.Fields.GetValueOrDefault("Period") == "BAL_1");
        Assert.Contains(statuses, x => x.Fields.GetValueOrDefault("Period") == "BAL_2");
        Assert.Contains(statuses, x => x.Fields.GetValueOrDefault("StatusMeaning") == "Conferido");
        Assert.Contains(statuses, x => x.Fields.GetValueOrDefault("IsHighlighted") == "true");

        var companyAggregate = records.Single(x =>
            x.Fields.GetValueOrDefault("__recordType") == "Aggregate" &&
            x.Fields.GetValueOrDefault("Metric") == "CompaniesByCurrentStatus" &&
            x.Fields.GetValueOrDefault("Scope") == "Global" &&
            x.Fields.GetValueOrDefault("Status") == "X");
        Assert.Equal("1", companyAggregate.Fields["Count"]);
        Assert.Empty(warnings);
    }

    [Fact]
    public void DeveSelecionarAbaMaisRecentePeloAno()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("CONF CONT 2024");
        workbook.AddWorksheet("CONF CONT 2026");
        workbook.AddWorksheet("Observações");
        var settings = new ExcelSourceSettings
        {
            WorksheetSelection = "LatestYear",
            WorksheetPattern = @"(?<year>20\d{2})"
        };

        var worksheets = ExcelSectionedMatrixParser.ResolveWorksheets(workbook, settings);

        Assert.Single(worksheets);
        Assert.Equal("CONF CONT 2026", worksheets[0].Name);
    }

    [Fact]
    public void DeveReconhecerEmpresasSemCodigoEmSecaoSemPeriodos()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("CONF CONT 2026");
        worksheet.Cell("B1").Value = "EMPRESAS MEI";
        worksheet.Cell("B2").Value = "Empresa MEI A";
        worksheet.Cell("B3").Value = "Empresa MEI B";

        var settings = new ExcelSourceSettings
        {
            Mode = "SectionedMatrix",
            Matrix = new ExcelMatrixSettings
            {
                StandaloneSectionTitles = "EMPRESAS MEI|SEM MOVIMENTO",
                SectionsWithoutPeriods = "EMPRESAS MEI|SEM MOVIMENTO"
            }
        };

        var records = ExcelSectionedMatrixParser.Parse(
            "planilha",
            worksheet,
            settings,
            new List<string>(),
            CancellationToken.None);

        var companies = records.Where(x => x.Fields.GetValueOrDefault("__recordType") == "Company").ToArray();
        Assert.Equal(2, companies.Length);
        Assert.All(companies, x => Assert.Equal("MEI", x.Fields["Section"]));
    }

    private static void AddCompany(
        IXLWorksheet worksheet,
        int row,
        string number,
        string company,
        string code,
        string collaborator,
        IReadOnlyList<string> statuses)
    {
        worksheet.Cell(row, 1).Value = number;
        worksheet.Cell(row, 2).Value = company;
        worksheet.Cell(row, 3).Value = code;
        worksheet.Cell(row, 4).Value = collaborator;
        for (var index = 0; index < statuses.Count; index++)
        {
            worksheet.Cell(row, 5 + index).Value = statuses[index];
        }
    }
}
