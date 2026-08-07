using AssetValueAnalyzer.Application.Assets.Imports;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.Infrastructure.Imports.Assets;

public sealed class ClosedXmlAssetFileParser : IAssetFileParser
{
    private const string ExpectedWorksheetName = "Varlık Tablosu";
    private const string ExpectedDateHeader = "Tarih";
    private const string ExpectedAmountHeader = "Varlık Tutarı";
    private const string InvalidTemplateCode = "InvalidAssetTemplate";
    private const string InvalidTemplateMessage =
        "Dosya beklenen Varlık Verisi şablonuna uygun değildir. Lütfen örnek dosyayı kontrol edip yeniden deneyin.";

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

            if (!workbook.TryGetWorksheet(ExpectedWorksheetName, out var worksheet))
            {
                return Invalid(InvalidTemplateCode, InvalidTemplateMessage);
            }

            if (!HasExpectedHeaders(worksheet))
            {
                return Invalid(InvalidTemplateCode, InvalidTemplateMessage);
            }

            return ParseRows(worksheet);
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

    private static AssetFileParseResult ParseRows(IXLWorksheet worksheet)
    {
        var values = new List<MonthlyAssetValueInput>();
        var errors = new List<AssetImportValidationError>();
        var seenMonths = new HashSet<DateOnly>();
        var lastRowNumber = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRowNumber; rowNumber++)
        {
            var dateCell = worksheet.Cell(rowNumber, 1);
            var amountCell = worksheet.Cell(rowNumber, 2);

            if (dateCell.IsEmpty() && amountCell.IsEmpty())
            {
                continue;
            }

            if (!dateCell.TryGetValue<DateTime>(out var dateValue))
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
                    "Varlık verisi Aralık 2021 veya sonrasına ait olmalıdır.",
                    rowNumber));
                continue;
            }

            if (!amountCell.TryGetValue<decimal>(out var amount))
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
                "Varlık dosyasında işlenecek veri satırı bulunamadı."));
        }

        return new AssetFileParseResult(values, errors);
    }

    private static bool HasExpectedHeaders(IXLWorksheet worksheet)
    {
        var dateHeader = worksheet.Cell(1, 1).GetString().Trim();
        var amountHeader = worksheet.Cell(1, 2).GetString().Trim();

        return string.Equals(dateHeader, ExpectedDateHeader, StringComparison.Ordinal) &&
               string.Equals(amountHeader, ExpectedAmountHeader, StringComparison.Ordinal);
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

    private static AssetFileParseResult Invalid(string code, string message) =>
        new([], [new AssetImportValidationError(code, message)]);
}
