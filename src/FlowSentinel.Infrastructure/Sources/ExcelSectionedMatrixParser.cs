using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FlowSentinel.Domain;

namespace FlowSentinel.Infrastructure.Sources;

internal static class ExcelSectionedMatrixParser
{
    internal static IReadOnlyList<IXLWorksheet> ResolveWorksheets(
        XLWorkbook workbook,
        ExcelSourceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(settings);

        var worksheets = workbook.Worksheets.ToArray();
        if (worksheets.Length == 0)
        {
            return [];
        }

        var selection = settings.WorksheetSelection?.Trim() ?? "Fixed";
        if (selection.Equals("AllMatching", StringComparison.OrdinalIgnoreCase))
        {
            var matching = worksheets
                .Where(x => IsWorksheetMatch(x.Name, settings.WorksheetPattern))
                .OrderBy(x => ExtractYear(x.Name, settings.WorksheetPattern) ?? int.MinValue)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return matching.Length > 0 ? matching : worksheets;
        }

        if (selection.Equals("LatestYear", StringComparison.OrdinalIgnoreCase))
        {
            var latest = worksheets
                .Select(x => new { Sheet = x, Year = ExtractYear(x.Name, settings.WorksheetPattern) })
                .Where(x => x.Year.HasValue)
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Sheet.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (latest is not null)
            {
                return [latest.Sheet];
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.Worksheet))
        {
            var fixedSheet = worksheets.FirstOrDefault(
                x => string.Equals(x.Name, settings.Worksheet, StringComparison.OrdinalIgnoreCase));
            if (fixedSheet is not null)
            {
                return [fixedSheet];
            }
        }

        return [worksheets[0]];
    }

    internal static IReadOnlyList<DataRecord> Parse(
        string sourceAlias,
        IXLWorksheet worksheet,
        ExcelSourceSettings settings,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        var matrix = settings.Matrix ?? new ExcelMatrixSettings();
        ValidateColumns(matrix);

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastColumn == 0)
        {
            warnings.Add($"A aba '{worksheet.Name}' está vazia.");
            return [];
        }

        var configuredPeriodLabels = SplitValues(matrix.PeriodLabels);
        var standaloneTitles = SplitValues(matrix.StandaloneSectionTitles);
        var sectionTitlePrefixes = SplitValues(matrix.SectionTitlePrefixes);
        var sectionNamePrefixesToRemove = SplitValues(matrix.SectionNamePrefixesToRemove);
        var sectionsWithoutPeriods = SplitValues(matrix.SectionsWithoutPeriods);
        var currentStatusExcludedPeriods = SplitValues(matrix.CurrentStatusExcludedPeriods);
        var records = new List<DataRecord>();
        var statusFields = new List<Dictionary<string, string?>>(Math.Max(100, lastRow));
        var companyFieldsForAggregates = new List<Dictionary<string, string?>>(Math.Max(20, lastRow));
        var seenEntityKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var year = ExtractYear(worksheet.Name, settings.WorksheetPattern)?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        SectionContext? currentSection = null;
        IReadOnlyList<PeriodColumn> inheritedPeriods = [];

        for (var rowNumber = 1; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            var numberText = GetText(row.Cell(matrix.NumberColumn));
            var sectionOrCompany = GetText(row.Cell(matrix.SectionColumn));
            var company = GetText(row.Cell(matrix.CompanyColumn));
            var code = GetText(row.Cell(matrix.CodeColumn));
            var collaborator = GetText(row.Cell(matrix.CollaboratorColumn));

            if (IsMatrixHeader(row, matrix, numberText, sectionOrCompany, lastColumn))
            {
                var periods = BuildPeriodColumns(row, matrix, lastColumn);
                if (periods.Count > 0)
                {
                    inheritedPeriods = periods;
                }

                currentSection = new SectionContext(
                    NormalizeSectionName(sectionOrCompany, sectionNamePrefixesToRemove),
                    sectionOrCompany,
                    rowNumber,
                    periods,
                    false);
                continue;
            }

            if (IsStandaloneSectionTitle(
                    row,
                    matrix,
                    numberText,
                    sectionOrCompany,
                    code,
                    collaborator,
                    standaloneTitles,
                    sectionTitlePrefixes))
            {
                var withoutPeriods = sectionsWithoutPeriods.Contains(sectionOrCompany.Trim());
                currentSection = new SectionContext(
                    NormalizeSectionName(sectionOrCompany, sectionNamePrefixesToRemove),
                    sectionOrCompany.Trim(),
                    rowNumber,
                    withoutPeriods ? [] : inheritedPeriods,
                    withoutPeriods);
                continue;
            }

            if (!IsDataRow(
                    row,
                    matrix,
                    company,
                    code,
                    collaborator,
                    currentSection?.AcceptCompanyOnlyRows == true,
                    configuredPeriodLabels))
            {
                continue;
            }

            currentSection ??= new SectionContext("Sem seção", "Sem seção", rowNumber, inheritedPeriods, false);
            var companyKey = !string.IsNullOrWhiteSpace(code)
                ? code.Trim()
                : NormalizeKey(company);
            if (string.IsNullOrWhiteSpace(companyKey))
            {
                companyKey = $"row-{rowNumber}";
            }

            var companyIdentity = $"{worksheet.Name}|{currentSection.Name}|{companyKey}";
            if (!seenEntityKeys.Add(companyIdentity))
            {
                warnings.Add($"A chave da entidade '{companyKey}' aparece mais de uma vez no grupo '{currentSection.Name}' da aba '{worksheet.Name}'.");
            }

            var periodValues = currentSection.Periods
                .Select(period => new PeriodValue(period, row.Cell(period.ColumnNumber), GetText(row.Cell(period.ColumnNumber))))
                .ToArray();
            var currentValue = SelectCurrentValue(
                periodValues,
                year,
                matrix.CurrentStatusMode,
                currentStatusExcludedPeriods,
                matrix.CalendarPeriodNumbers);
            var currentStatus = currentValue?.Status ?? string.Empty;
            var currentPeriod = currentValue?.Period.Key ?? string.Empty;
            var currentMeaning = GetStatusMeaning(matrix, currentStatus);

            if (matrix.GenerateCompanyRecords)
            {
                var companyRecordKey = $"entity|{worksheet.Name}|{currentSection.Name}|{companyKey}";
                var companyFields = CreateBaseFields(
                    companyRecordKey,
                    "Entity",
                    worksheet.Name,
                    year,
                    currentSection,
                    companyKey,
                    company,
                    code,
                    collaborator,
                    rowNumber);
                companyFields["Number"] = numberText;
                companyFields["Period"] = null;
                companyFields["Status"] = null;
                companyFields["CurrentPeriod"] = string.IsNullOrWhiteSpace(currentPeriod) ? null : currentPeriod;
                companyFields["CurrentStatus"] = string.IsNullOrWhiteSpace(currentStatus) ? null : currentStatus;
                companyFields["CurrentStatusDisplay"] = string.IsNullOrWhiteSpace(currentStatus) ? "(vazio)" : currentStatus;
                companyFields["CurrentValue"] = companyFields["CurrentStatus"];
                companyFields["CurrentValueDisplay"] = companyFields["CurrentStatusDisplay"];
                companyFields["StatusMeaning"] = string.IsNullOrWhiteSpace(currentMeaning) ? null : currentMeaning;
                companyFields["ValueMeaning"] = companyFields["StatusMeaning"];
                records.Add(CreateRecord(sourceAlias, companyRecordKey, companyFields));
                companyFieldsForAggregates.Add(companyFields);
            }

            foreach (var item in periodValues)
            {
                var status = item.Status;
                if (string.IsNullOrWhiteSpace(status) && !matrix.IncludeBlankStatuses)
                {
                    continue;
                }

                var formatting = matrix.IncludeFormatting ? GetCellFormatting(item.Cell) : CellFormatting.Empty;
                var recordKey = $"status|{worksheet.Name}|{currentSection.Name}|{companyKey}|{item.Period.Key}";
                var fields = CreateBaseFields(
                    recordKey,
                    "Status",
                    worksheet.Name,
                    year,
                    currentSection,
                    companyKey,
                    company,
                    code,
                    collaborator,
                    rowNumber);
                fields["Number"] = numberText;
                fields["Period"] = item.Period.Key;
                fields["PeriodBase"] = item.Period.BaseLabel;
                fields["PeriodOccurrence"] = item.Period.Occurrence.ToString(CultureInfo.InvariantCulture);
                fields["Status"] = string.IsNullOrWhiteSpace(status) ? null : status;
                fields["StatusDisplay"] = string.IsNullOrWhiteSpace(status) ? "(vazio)" : status;
                fields["Value"] = fields["Status"];
                fields["ValueDisplay"] = fields["StatusDisplay"];
                fields["StatusMeaning"] = GetStatusMeaning(matrix, status);
                fields["ValueMeaning"] = fields["StatusMeaning"];
                fields["CurrentPeriod"] = string.IsNullOrWhiteSpace(currentPeriod) ? null : currentPeriod;
                fields["CurrentStatus"] = string.IsNullOrWhiteSpace(currentStatus) ? null : currentStatus;
                fields["CurrentValue"] = fields["CurrentStatus"];
                fields["CellAddress"] = item.Cell.Address?.ToString() ?? string.Empty;
                fields["ColumnNumber"] = item.Period.ColumnNumber.ToString(CultureInfo.InvariantCulture);
                fields["FillColor"] = formatting.FillColor;
                fields["FillPattern"] = formatting.Pattern;
                fields["IsHighlighted"] = formatting.IsHighlighted ? "true" : "false";
                statusFields.Add(fields);
                records.Add(CreateRecord(sourceAlias, recordKey, fields));
            }
        }

        if (matrix.GenerateAggregateRecords)
        {
            records.AddRange(CreateAggregateRecords(sourceAlias, statusFields, companyFieldsForAggregates, matrix));
        }

        if (records.Count == 0)
        {
            warnings.Add($"Nenhum registro foi reconhecido na aba '{worksheet.Name}'. Revise o marcador do cabeçalho e as colunas configuradas.");
        }

        return records;
    }

    internal static WorkbookWorksheetData BuildVisual(
        IXLWorksheet worksheet,
        int maximumRows = 300,
        int maximumColumns = 50)
    {
        var lastRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 0, maximumRows);
        var lastColumn = Math.Min(worksheet.LastColumnUsed()?.ColumnNumber() ?? 0, maximumColumns);
        var cells = new List<WorkbookWorksheetCell>(Math.Max(0, lastRow * lastColumn));
        for (var row = 1; row <= lastRow; row++)
        {
            for (var column = 1; column <= lastColumn; column++)
            {
                var cell = worksheet.Cell(row, column);
                var formatting = GetCellFormatting(cell);
                cells.Add(new WorkbookWorksheetCell(
                    row,
                    column,
                    cell.Address?.ToString() ?? string.Empty,
                    GetText(cell),
                    formatting.FillColor,
                    formatting.IsHighlighted,
                    cell.Style.Font.Bold,
                    cell.Style.Font.Italic));
            }
        }

        var columnWidths = Enumerable.Range(1, lastColumn)
            .ToDictionary(column => column, column => worksheet.Column(column).Width);
        var rowHeights = Enumerable.Range(1, lastRow)
            .ToDictionary(row => row, row => worksheet.Row(row).Height);
        return new WorkbookWorksheetData(worksheet.Name, lastRow, lastColumn, cells, columnWidths, rowHeights);
    }

    private static IReadOnlyCollection<DataRecord> CreateAggregateRecords(
        string sourceAlias,
        IReadOnlyCollection<Dictionary<string, string?>> statusFields,
        IReadOnlyCollection<Dictionary<string, string?>> companyFields,
        ExcelMatrixSettings settings)
    {
        var results = new List<DataRecord>();
        AddStatusCellAggregates(results, sourceAlias, statusFields, "Global", _ => "Todos");
        AddCompanyStatusAggregates(results, sourceAlias, companyFields, "Global", settings, _ => "Todos");

        if (settings.AggregateBySection)
        {
            AddStatusCellAggregates(results, sourceAlias, statusFields, "Category", x => x.GetValueOrDefault("Category") ?? x.GetValueOrDefault("Section") ?? "Sem grupo");
            AddCompanyStatusAggregates(results, sourceAlias, companyFields, "Category", settings, x => x.GetValueOrDefault("Category") ?? x.GetValueOrDefault("Section") ?? "Sem grupo");
        }

        if (settings.AggregateByCollaborator)
        {
            AddStatusCellAggregates(results, sourceAlias, statusFields, "Owner", x => x.GetValueOrDefault("Owner") ?? x.GetValueOrDefault("Collaborator") ?? "Sem responsável");
            AddCompanyStatusAggregates(results, sourceAlias, companyFields, "Owner", settings, x => x.GetValueOrDefault("Owner") ?? x.GetValueOrDefault("Collaborator") ?? "Sem responsável");
        }

        return results;
    }

    private static void AddStatusCellAggregates(
        ICollection<DataRecord> destination,
        string sourceAlias,
        IEnumerable<Dictionary<string, string?>> statusFields,
        string scope,
        Func<Dictionary<string, string?>, string> groupSelector)
    {
        var groups = statusFields.GroupBy(x => new
        {
            Worksheet = x.GetValueOrDefault("Worksheet") ?? string.Empty,
            Year = x.GetValueOrDefault("Year") ?? string.Empty,
            Group = groupSelector(x),
            Period = x.GetValueOrDefault("Period") ?? string.Empty,
            Status = x.GetValueOrDefault("StatusDisplay") ?? "(vazio)",
            Meaning = x.GetValueOrDefault("StatusMeaning") ?? string.Empty
        });

        foreach (var group in groups)
        {
            AddAggregateRecord(
                destination,
                sourceAlias,
                metric: "ValuesByPeriod",
                unit: "Células",
                scope,
                group.Key.Worksheet,
                group.Key.Year,
                group.Key.Group,
                group.Key.Period,
                group.Key.Status,
                group.Key.Meaning,
                group.Count());
        }
    }

    private static void AddCompanyStatusAggregates(
        ICollection<DataRecord> destination,
        string sourceAlias,
        IEnumerable<Dictionary<string, string?>> companyFields,
        string scope,
        ExcelMatrixSettings settings,
        Func<Dictionary<string, string?>, string> groupSelector)
    {
        var groups = companyFields.GroupBy(x => new
        {
            Worksheet = x.GetValueOrDefault("Worksheet") ?? string.Empty,
            Year = x.GetValueOrDefault("Year") ?? string.Empty,
            Group = groupSelector(x),
            Status = x.GetValueOrDefault("CurrentStatusDisplay") ?? "(vazio)",
            Meaning = x.GetValueOrDefault("StatusMeaning") ?? string.Empty
        });

        foreach (var group in groups)
        {
            var distinctCompanies = group
                .Select(x => x.GetValueOrDefault("EntityKey") ?? x.GetValueOrDefault("CompanyKey") ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            AddAggregateRecord(
                destination,
                sourceAlias,
                metric: "EntitiesByCurrentValue",
                unit: string.IsNullOrWhiteSpace(settings.EntityPluralName) ? "Registros" : settings.EntityPluralName.Trim(),
                scope,
                group.Key.Worksheet,
                group.Key.Year,
                group.Key.Group,
                "Atual",
                group.Key.Status,
                group.Key.Meaning,
                distinctCompanies);
        }
    }

    private static void AddAggregateRecord(
        ICollection<DataRecord> destination,
        string sourceAlias,
        string metric,
        string unit,
        string scope,
        string worksheet,
        string year,
        string group,
        string period,
        string statusDisplay,
        string statusMeaning,
        int count)
    {
        var recordKey = $"aggregate|{metric}|{scope}|{worksheet}|{group}|{period}|{statusDisplay}";
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["__recordKey"] = recordKey,
            ["__recordType"] = "Aggregate",
            ["Worksheet"] = worksheet,
            ["Year"] = year,
            ["Metric"] = metric,
            ["Unit"] = unit,
            ["Scope"] = scope,
            ["Group"] = group,
            ["Period"] = period,
            ["Status"] = statusDisplay == "(vazio)" ? null : statusDisplay,
            ["StatusDisplay"] = statusDisplay,
            ["StatusMeaning"] = string.IsNullOrWhiteSpace(statusMeaning) ? null : statusMeaning,
            ["Value"] = statusDisplay == "(vazio)" ? null : statusDisplay,
            ["ValueDisplay"] = statusDisplay,
            ["ValueMeaning"] = string.IsNullOrWhiteSpace(statusMeaning) ? null : statusMeaning,
            ["Count"] = count.ToString(CultureInfo.InvariantCulture)
        };
        destination.Add(CreateRecord(sourceAlias, recordKey, fields));
    }

    private static Dictionary<string, string?> CreateBaseFields(
        string recordKey,
        string recordType,
        string worksheet,
        string year,
        SectionContext section,
        string companyKey,
        string company,
        string code,
        string collaborator,
        int rowNumber) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["__recordKey"] = recordKey,
        ["__recordType"] = recordType,
        ["Worksheet"] = worksheet,
        ["Year"] = year,
        ["Section"] = section.Name,
        ["SectionTitle"] = section.Title,
        ["Regime"] = section.Name,
        ["CompanyKey"] = companyKey,
        ["Company"] = company,
        ["Code"] = string.IsNullOrWhiteSpace(code) ? null : code,
        ["Collaborator"] = string.IsNullOrWhiteSpace(collaborator) ? null : collaborator,
        ["EntityKey"] = companyKey,
        ["Entity"] = company,
        ["Owner"] = string.IsNullOrWhiteSpace(collaborator) ? null : collaborator,
        ["Category"] = section.Name,
        ["RowNumber"] = rowNumber.ToString(CultureInfo.InvariantCulture)
    };

    private static DataRecord CreateRecord(
        string sourceAlias,
        string recordKey,
        IReadOnlyDictionary<string, string?> fields) => new()
    {
        Key = recordKey,
        SourceAlias = sourceAlias,
        Fields = fields,
        CollectedAt = DateTimeOffset.Now,
        Fingerprint = ComputeFingerprint(fields)
    };

    private static PeriodValue? SelectCurrentValue(
        IReadOnlyList<PeriodValue> periodValues,
        string yearText,
        string mode,
        ISet<string> excludedPeriods,
        IReadOnlyDictionary<string, int>? calendarPeriodNumbers)
    {
        var candidates = periodValues
            .Where(x => !string.IsNullOrWhiteSpace(x.Status) && !excludedPeriods.Contains(x.Period.BaseLabel))
            .ToArray();
        if (!string.Equals(mode, "CalendarPeriod", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(yearText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var worksheetYear))
        {
            return candidates.LastOrDefault();
        }

        var today = DateTime.Today;
        if (worksheetYear > today.Year)
        {
            return null;
        }
        if (worksheetYear < today.Year)
        {
            return candidates.LastOrDefault();
        }

        return candidates.LastOrDefault(x =>
        {
            var month = ResolveCalendarPeriodNumber(x.Period.BaseLabel, calendarPeriodNumbers);
            return month is null || month <= today.Month;
        });
    }

    private static int? ResolveCalendarPeriodNumber(
        string label,
        IReadOnlyDictionary<string, int>? configuredNumbers)
    {
        if (configuredNumbers is not null)
        {
            var configured = configuredNumbers.FirstOrDefault(
                x => string.Equals(x.Key.Trim(), label.Trim(), StringComparison.OrdinalIgnoreCase));
            if (configured.Key is not null && configured.Value is >= 1 and <= 12)
            {
                return configured.Value;
            }
        }

        return int.TryParse(label, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) &&
               numeric is >= 1 and <= 12
            ? numeric
            : null;
    }

    private static bool IsMatrixHeader(
        IXLRow row,
        ExcelMatrixSettings matrix,
        string numberText,
        string sectionText,
        int lastColumn)
    {
        if (!string.IsNullOrWhiteSpace(matrix.HeaderMarker) &&
            string.Equals(numberText, matrix.HeaderMarker.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(matrix.HeaderTextContains) &&
               sectionText.Contains(matrix.HeaderTextContains.Trim(), StringComparison.OrdinalIgnoreCase) &&
               BuildPeriodColumns(row, matrix, lastColumn).Count > 0;
    }

    private static bool IsStandaloneSectionTitle(
        IXLRow row,
        ExcelMatrixSettings matrix,
        string numberText,
        string sectionText,
        string code,
        string collaborator,
        ISet<string> configuredTitles,
        ISet<string> configuredPrefixes)
    {
        if (!matrix.AutoDetectStandaloneSections || string.IsNullOrWhiteSpace(sectionText))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(numberText) || !string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(collaborator))
        {
            return false;
        }

        var hasPeriodValue = Enumerable.Range(matrix.FirstPeriodColumn, Math.Max(0, matrix.LastPeriodColumn - matrix.FirstPeriodColumn + 1))
            .Any(column => !string.IsNullOrWhiteSpace(GetText(row.Cell(column))));
        if (hasPeriodValue)
        {
            return false;
        }

        return configuredTitles.Contains(sectionText.Trim()) ||
               configuredPrefixes.Any(prefix =>
                   !string.IsNullOrWhiteSpace(prefix) &&
                   sectionText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDataRow(
        IXLRow row,
        ExcelMatrixSettings matrix,
        string company,
        string code,
        string collaborator,
        bool acceptCompanyOnlyRow,
        ISet<string> configuredPeriodLabels)
    {
        if (string.IsNullOrWhiteSpace(company) || configuredPeriodLabels.Contains(company.Trim()))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(collaborator) || acceptCompanyOnlyRow)
        {
            return true;
        }

        return Enumerable.Range(matrix.FirstPeriodColumn, Math.Max(0, matrix.LastPeriodColumn - matrix.FirstPeriodColumn + 1))
                   .Any(column => !string.IsNullOrWhiteSpace(GetText(row.Cell(column)))) ||
               !string.IsNullOrWhiteSpace(GetText(row.Cell(matrix.NumberColumn)));
    }

    private static IReadOnlyList<PeriodColumn> BuildPeriodColumns(
        IXLRow row,
        ExcelMatrixSettings matrix,
        int lastUsedColumn)
    {
        var lastColumn = Math.Min(matrix.LastPeriodColumn, lastUsedColumn);
        var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var raw = new List<(int Column, string Label)>();
        for (var column = matrix.FirstPeriodColumn; column <= lastColumn; column++)
        {
            var label = GetText(row.Cell(column)).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            raw.Add((column, label));
            occurrences[label] = occurrences.GetValueOrDefault(label) + 1;
        }

        var running = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var periods = new List<PeriodColumn>();
        foreach (var item in raw)
        {
            running[item.Label] = running.GetValueOrDefault(item.Label) + 1;
            var occurrence = running[item.Label];
            var key = occurrences[item.Label] > 1 ? $"{item.Label}_{occurrence}" : item.Label;
            periods.Add(new PeriodColumn(item.Column, key, item.Label, occurrence));
        }

        return periods;
    }

    private static bool IsWorksheetMatch(string value, string? pattern)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(pattern) &&
                   Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static int? ExtractYear(string value, string? pattern)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
            if (!match.Success)
            {
                return null;
            }

            var text = match.Groups["year"].Success ? match.Groups["year"].Value : match.Value;
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ? year : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NormalizeSectionName(string value, ISet<string> prefixesToRemove)
    {
        var normalized = value.Trim();
        foreach (var prefix in prefixesToRemove.OrderByDescending(x => x.Length))
        {
            if (!string.IsNullOrWhiteSpace(prefix) &&
                normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }

        return string.IsNullOrWhiteSpace(normalized) ? "Sem seção" : normalized;
    }

    private static string NormalizeKey(string value)
    {
        var normalized = value.Trim().ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string GetStatusMeaning(ExcelMatrixSettings matrix, string status)
    {
        if (string.IsNullOrWhiteSpace(status) || matrix.StatusLabels is null || matrix.StatusLabels.Count == 0)
        {
            return string.Empty;
        }

        var item = matrix.StatusLabels.FirstOrDefault(
            x => string.Equals(x.Key.Trim(), status.Trim(), StringComparison.OrdinalIgnoreCase));
        return item.Key is null ? string.Empty : item.Value?.Trim() ?? string.Empty;
    }

    private static string GetText(IXLCell cell) => cell.GetFormattedString().Trim();

    private static CellFormatting GetCellFormatting(IXLCell cell)
    {
        var pattern = cell.Style.Fill.PatternType.ToString();
        var background = ExtractColor(cell.Style.Fill.BackgroundColor);
        var foreground = ExtractColor(cell.Style.Fill.PatternColor);
        var fill = SelectVisibleFillColor(pattern, background, foreground);
        var highlighted = IsHighlightColor(background) || IsHighlightColor(foreground);
        return new CellFormatting(fill, pattern, highlighted);
    }

    private static string SelectVisibleFillColor(string pattern, string background, string foreground)
    {
        if (string.Equals(pattern, "Solid", StringComparison.OrdinalIgnoreCase) && IsUsableColor(foreground))
        {
            return foreground;
        }

        if (IsUsableColor(background))
        {
            return background;
        }

        return IsUsableColor(foreground) ? foreground : string.Empty;
    }

    private static bool IsHighlightColor(string value) =>
        IsUsableColor(value) &&
        !value.Equals("#FFFFFF", StringComparison.OrdinalIgnoreCase) &&
        !value.Equals("#FFFFFFFF", StringComparison.OrdinalIgnoreCase) &&
        !value.Equals("#000000", StringComparison.OrdinalIgnoreCase) &&
        !value.Equals("#FF000000", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsableColor(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Equals("ColorType: Indexed", StringComparison.OrdinalIgnoreCase) &&
        !value.Equals("#00000000", StringComparison.OrdinalIgnoreCase);

    private static string ExtractColor(object colorObject)
    {
        try
        {
            var property = colorObject.GetType().GetProperty("Color");
            if (property?.GetValue(colorObject) is System.Drawing.Color color)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            var text = colorObject.ToString() ?? string.Empty;
            var match = Regex.Match(text, "#[0-9A-Fa-f]{6,8}");
            if (match.Success)
            {
                var hex = match.Value;
                return hex.Length == 9 ? $"#{hex[3..]}" : hex;
            }

            return text;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static HashSet<string> SplitValues(string? value) =>
        (value ?? string.Empty)
            .Split(['|', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);


    private static void ValidateColumns(ExcelMatrixSettings matrix)
    {
        var columns = new[]
        {
            matrix.NumberColumn, matrix.SectionColumn, matrix.CompanyColumn,
            matrix.CodeColumn, matrix.CollaboratorColumn, matrix.FirstPeriodColumn,
            matrix.LastPeriodColumn
        };
        if (columns.Any(x => x < 1))
        {
            throw new InvalidOperationException("As colunas da matriz Excel devem ser maiores que zero.");
        }

        if (matrix.LastPeriodColumn < matrix.FirstPeriodColumn)
        {
            throw new InvalidOperationException("A última coluna de período não pode ser anterior à primeira.");
        }
    }

    private static string ComputeFingerprint(IReadOnlyDictionary<string, string?> fields)
    {
        var text = string.Join("\u001f", fields.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key}={x.Value}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private sealed record SectionContext(
        string Name,
        string Title,
        int HeaderRow,
        IReadOnlyList<PeriodColumn> Periods,
        bool AcceptCompanyOnlyRows);

    private sealed record PeriodColumn(int ColumnNumber, string Key, string BaseLabel, int Occurrence);
    private sealed record PeriodValue(PeriodColumn Period, IXLCell Cell, string Status);
    private sealed record CellFormatting(string FillColor, string Pattern, bool IsHighlighted)
    {
        public static readonly CellFormatting Empty = new(string.Empty, string.Empty, false);
    }

    internal sealed record WorkbookWorksheetData(
        string Worksheet,
        int RowCount,
        int ColumnCount,
        IReadOnlyList<WorkbookWorksheetCell> Cells,
        IReadOnlyDictionary<int, double> ColumnWidths,
        IReadOnlyDictionary<int, double> RowHeights);

    internal sealed record WorkbookWorksheetCell(
        int Row,
        int Column,
        string Address,
        string Value,
        string FillColor,
        bool IsHighlighted,
        bool Bold,
        bool Italic);
}
