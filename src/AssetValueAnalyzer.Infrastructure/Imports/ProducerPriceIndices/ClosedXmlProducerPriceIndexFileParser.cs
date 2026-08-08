using System.Globalization;
using System.Text;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;

public sealed class ClosedXmlProducerPriceIndexFileParser
    : IProducerPriceIndexFileParser
{
    private const string InvalidTemplateCode = "InvalidProducerPriceIndexTemplate";
    private const string InvalidTemplateMessage =
        "Dosya beklenen Endeks Verisi şablonuna uygun değildir. Lütfen örnek dosyayı kontrol edip yeniden deneyin.";

    private static readonly string[] YearAliases = ["yil", "year"];

    private static readonly string[][] MonthAliases =
    [
        ["ocak", "january", "jan"],
        ["subat", "february", "feb"],
        ["mart", "march", "mar"],
        ["nisan", "april", "apr"],
        ["mayis", "may"],
        ["haziran", "june", "jun"],
        ["temmuz", "july", "jul"],
        ["agustos", "august", "aug"],
        ["eylul", "september", "sep"],
        ["ekim", "october", "oct"],
        ["kasim", "november", "nov"],
        ["aralik", "december", "dec"]
    ];

    public bool CanParse(string fileExtension) =>
        string.Equals(fileExtension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public async Task<ProducerPriceIndexFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        if (!HasZipSignature(buffer))
        {
            return Invalid(InvalidTemplateCode, InvalidTemplateMessage);
        }

        buffer.Position = 0;

        try
        {
            using var workbook = new XLWorkbook(buffer);

            if (!TryFindIndexWorksheet(workbook, out var worksheet, out var headerRow))
            {
                return Invalid(InvalidTemplateCode, InvalidTemplateMessage);
            }

            return ParseRows(worksheet, headerRow + 1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Invalid(InvalidTemplateCode, InvalidTemplateMessage);
        }
    }

    private static ProducerPriceIndexFileParseResult ParseRows(
        IXLWorksheet worksheet,
        int firstPossibleDataRow)
    {
        var values = new List<MonthlyProducerPriceIndexInput>();
        var errors = new List<ProducerPriceIndexImportValidationError>();
        var seenYears = new HashSet<int>();
        var seenMonths = new HashSet<DateOnly>();
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? firstPossibleDataRow;

        for (var rowNumber = firstPossibleDataRow; rowNumber <= lastRowNumber; rowNumber++)
        {
            var yearCell = worksheet.Cell(rowNumber, 1);

            if (!TryReadYear(yearCell, out var year))
            {
                continue;
            }

            if (!seenYears.Add(year))
            {
                errors.Add(new(
                    "DuplicateYear",
                    $"{year} yılı dosyada birden fazla kez bulunuyor.",
                    rowNumber,
                    1));
                continue;
            }

            if (HasUnexpectedDataColumns(worksheet, rowNumber))
            {
                errors.Add(new(
                    "UnexpectedColumns",
                    "Endeks veri satırları yıl ve Ocak-Aralık ay kolonları dışında veri içermemelidir.",
                    rowNumber));
                continue;
            }

            for (var monthNumber = 1; monthNumber <= 12; monthNumber++)
            {
                var month = new DateOnly(year, monthNumber, 1);
                var columnNumber = monthNumber + 1;
                var indexCell = worksheet.Cell(rowNumber, columnNumber);

                if (indexCell.IsEmpty())
                {
                    continue;
                }

                if (indexCell.DataType != XLDataType.Number ||
                    !indexCell.TryGetValue<decimal>(out var indexValue))
                {
                    errors.Add(new(
                        "InvalidIndexValue",
                        $"{month:yyyy-MM} endeks değeri sayısal olmalıdır.",
                        rowNumber,
                        columnNumber));
                    continue;
                }

                if (indexValue <= 0)
                {
                    errors.Add(new(
                        "NonPositiveIndexValue",
                        $"{month:yyyy-MM} endeks değeri sıfırdan büyük olmalıdır.",
                        rowNumber,
                        columnNumber));
                    continue;
                }

                if (!seenMonths.Add(month))
                {
                    errors.Add(new(
                        "DuplicateMonth",
                        $"{month:yyyy-MM} ayı dosyada birden fazla kez bulunuyor.",
                        rowNumber,
                        columnNumber));
                    continue;
                }

                values.Add(new MonthlyProducerPriceIndexInput(month, indexValue));
            }
        }

        if (values.Count == 0 && errors.Count == 0)
        {
            errors.Add(new(
                "NoDataRows",
                "Endeks dosyasında işlenebilecek aylık veri bulunamadı."));
        }

        return new ProducerPriceIndexFileParseResult(
            values.OrderBy(value => value.Month).ToArray(),
            errors);
    }

    private static bool TryFindIndexWorksheet(
        XLWorkbook workbook,
        out IXLWorksheet worksheet,
        out int headerRow)
    {
        foreach (var candidate in workbook.Worksheets)
        {
            var lastRowNumber = candidate.LastRowUsed()?.RowNumber() ?? 0;

            for (var rowNumber = 1; rowNumber <= lastRowNumber; rowNumber++)
            {
                if (!HasExpectedHeaders(candidate, rowNumber))
                {
                    continue;
                }

                worksheet = candidate;
                headerRow = rowNumber;
                return true;
            }
        }

        worksheet = null!;
        headerRow = 0;
        return false;
    }

    private static bool HasExpectedHeaders(IXLWorksheet worksheet, int rowNumber)
    {
        var yearHeader = Normalize(worksheet.Cell(rowNumber, 1).GetString());

        if (!YearAliases.Contains(yearHeader, StringComparer.Ordinal))
        {
            return false;
        }

        for (var monthNumber = 1; monthNumber <= 12; monthNumber++)
        {
            var header = Normalize(worksheet.Cell(rowNumber, monthNumber + 1).GetString());

            if (!MonthAliases[monthNumber - 1].Contains(header, StringComparer.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadYear(IXLCell cell, out int year)
    {
        if (cell.DataType == XLDataType.Number &&
            cell.TryGetValue<decimal>(out var numericYear) &&
            numericYear == decimal.Truncate(numericYear) &&
            numericYear is >= 1 and <= 9999)
        {
            year = decimal.ToInt32(numericYear);
            return true;
        }

        return int.TryParse(
            cell.GetString(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out year) && year is >= 1 and <= 9999;
    }

    private static bool HasUnexpectedDataColumns(
        IXLWorksheet worksheet,
        int rowNumber) =>
        worksheet.Row(rowNumber)
            .CellsUsed()
            .Any(cell => cell.Address.ColumnNumber > 13 && !cell.IsEmpty());

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character == 'ı'
                    ? 'i'
                    : char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static bool HasZipSignature(MemoryStream stream)
    {
        if (stream.Length < 4)
        {
            return false;
        }

        var bytes = stream.GetBuffer();

        return bytes[0] == (byte)'P' &&
               bytes[1] == (byte)'K' &&
               bytes[2] == 3 &&
               bytes[3] == 4;
    }

    private static ProducerPriceIndexFileParseResult Invalid(
        string code,
        string message) =>
        new([], [new ProducerPriceIndexImportValidationError(code, message)]);
}
