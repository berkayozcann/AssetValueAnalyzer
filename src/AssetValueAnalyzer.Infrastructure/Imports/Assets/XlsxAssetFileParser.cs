using AssetValueAnalyzer.Application.Assets.Imports;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.Infrastructure.Imports.Assets;

public sealed class XlsxAssetFileParser : IAssetFileParser
{
    private const string InvalidTemplateCode = "InvalidAssetTemplate";
    private const string InvalidTemplateMessage =
        "Dosya beklenen Aylık Varlık Verisi şablonuna uygun değildir. Lütfen örnek dosyayı kontrol edip yeniden deneyin.";

    public bool CanParse(string fileExtension) =>
        string.Equals(fileExtension, ".xlsx", StringComparison.OrdinalIgnoreCase);

    public async Task<AssetFileParseResult> ParseAsync(
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

            if (!TryFindAssetWorksheet(workbook, out var worksheet, out var firstDataRow))
            {
                return Invalid(InvalidTemplateCode, InvalidTemplateMessage);
            }

            return ParseRows(worksheet, firstDataRow);
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

    private static AssetFileParseResult ParseRows(
        IXLWorksheet worksheet,
        int firstDataRow)
    {
        var values = new List<MonthlyAssetValueInput>();
        var errors = new List<AssetImportValidationError>();
        var seenMonths = new HashSet<DateOnly>();
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = firstDataRow; rowNumber <= lastRowNumber; rowNumber++)
        {
            var dateCell = worksheet.Cell(rowNumber, 1);
            var amountCell = worksheet.Cell(rowNumber, 2);

            if (HasUnexpectedDataColumns(worksheet, rowNumber))
            {
                errors.Add(new(
                    "UnexpectedColumns",
                    "Aylık Varlık Verisi dosyasındaki satırlar yalnızca ay ve varlık tutarı olmak üzere iki kolon içermelidir.",
                    rowNumber));
                continue;
            }

            if (dateCell.IsEmpty() && amountCell.IsEmpty())
            {
                continue;
            }

            if (!IsExcelDateCell(dateCell) ||
                !dateCell.TryGetValue<DateTime>(out var dateValue))
            {
                errors.Add(new(
                    "InvalidMonth",
                    "Tarih hücresi geçerli bir Excel tarihi olmalıdır.",
                    rowNumber));
                continue;
            }

            var month = new DateOnly(dateValue.Year, dateValue.Month, 1);

            if (month < AssetImportRules.EarliestSupportedMonth)
            {
                errors.Add(new(
                    "MonthOutOfRange",
                    "Aylık Varlık Verisi Aralık 2021 veya sonrasına ait olmalıdır.",
                    rowNumber));
                continue;
            }

            if (amountCell.DataType != XLDataType.Number ||
                !amountCell.TryGetValue<decimal>(out var amount))
            {
                errors.Add(new(
                    "InvalidAmount",
                    "Varlık tutarı sayısal bir değer olmalıdır.",
                    rowNumber));
                continue;
            }

            if (amount < 0)
            {
                errors.Add(new(
                    "NegativeAmount",
                    "Varlık tutarı negatif olamaz.",
                    rowNumber));
                continue;
            }

            if (!seenMonths.Add(month))
            {
                errors.Add(new(
                    "DuplicateMonth",
                    $"{month:yyyy-MM} ayı dosyada birden fazla kez bulunuyor.",
                    rowNumber));
                continue;
            }

            values.Add(new MonthlyAssetValueInput(month, amount));
        }

        if (values.Count == 0 && errors.Count == 0)
        {
            errors.Add(new(
                "NoDataRows",
                "Aylık Varlık Verisi dosyasında işlenecek veri satırı bulunamadı."));
        }

        return new AssetFileParseResult(values, errors);
    }

    private static bool TryFindAssetWorksheet(
        XLWorkbook workbook,
        out IXLWorksheet worksheet,
        out int firstDataRow)
    {
        foreach (var candidate in workbook.Worksheets)
        {
            var lastRowNumber = candidate.LastRowUsed()?.RowNumber() ?? 0;

            for (var rowNumber = 1; rowNumber <= lastRowNumber; rowNumber++)
            {
                if (!HasAssetDataStart(candidate, rowNumber))
                {
                    continue;
                }

                worksheet = candidate;
                firstDataRow = rowNumber;
                return true;
            }
        }

        worksheet = null!;
        firstDataRow = 0;
        return false;
    }

    private static bool HasAssetDataStart(IXLWorksheet worksheet, int rowNumber)
    {
        var dateCell = worksheet.Cell(rowNumber, 1);

        return IsExcelDateCell(dateCell) &&
               dateCell.TryGetValue<DateTime>(out _);
    }

    private static bool IsExcelDateCell(IXLCell cell)
    {
        if (cell.DataType == XLDataType.DateTime)
        {
            return true;
        }

        if (cell.DataType != XLDataType.Number)
        {
            return false;
        }

        var numberFormatId = cell.Style.DateFormat.NumberFormatId;

        if (numberFormatId is >= 14 and <= 22 or
            >= 27 and <= 36 or
            >= 50 and <= 58)
        {
            return true;
        }

        return HasDateFormatToken(cell.Style.DateFormat.Format);
    }

    private static bool HasDateFormatToken(string format)
    {
        var insideQuotes = false;
        var insideBrackets = false;

        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];

            if (character == '\\')
            {
                index++;
                continue;
            }

            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (!insideQuotes && character == '[')
            {
                insideBrackets = true;
                continue;
            }

            if (!insideQuotes && character == ']')
            {
                insideBrackets = false;
                continue;
            }

            if (!insideQuotes && !insideBrackets &&
                character is 'd' or 'D' or 'y' or 'Y')
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnexpectedDataColumns(
        IXLWorksheet worksheet,
        int rowNumber) =>
        worksheet.Row(rowNumber)
            .CellsUsed()
            .Any(cell => cell.Address.ColumnNumber > 2 && !cell.IsEmpty());

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

    private static AssetFileParseResult Invalid(string code, string message) =>
        new([], [new AssetImportValidationError(code, message)]);
}
