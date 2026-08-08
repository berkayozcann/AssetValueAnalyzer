using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AssetValueAnalyzer.Application.Assets.Imports;

namespace AssetValueAnalyzer.Infrastructure.Imports.Assets;

public sealed class XmlAssetFileParser : IAssetFileParser
{
    private const string InvalidTemplateCode = "InvalidAssetTemplate";
    private const string InvalidTemplateMessage =
        "Dosya beklenen Varlık Verisi XML şablonuna uygun değildir. Lütfen örnek dosyayı kontrol edip yeniden deneyin.";

    public bool CanParse(string fileExtension) =>
        string.Equals(fileExtension, ".xml", StringComparison.OrdinalIgnoreCase);

    public async Task<AssetFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            MaxCharactersInDocument = ReadAssetValuesService.MaxFileSize
        };

        try
        {
            using var reader = XmlReader.Create(stream, settings);
            var document = await XDocument.LoadAsync(
                reader,
                LoadOptions.SetLineInfo,
                cancellationToken);

            return ParseDocument(document);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is XmlException or InvalidOperationException or ArgumentException)
        {
            return InvalidTemplate();
        }
    }

    private static AssetFileParseResult ParseDocument(XDocument document)
    {
        var root = document.Root;

        if (root is null ||
            root.Name != "AssetValues" ||
            root.Attribute("version")?.Value != "1.0" ||
            root.Attributes().Any(attribute => attribute.Name != "version"))
        {
            return InvalidTemplate();
        }

        var values = new List<MonthlyAssetValueInput>();
        var errors = new List<AssetImportValidationError>();
        var seenMonths = new HashSet<DateOnly>();

        foreach (var element in root.Elements())
        {
            if (!HasExpectedRecordShape(element))
            {
                errors.Add(new(
                    InvalidTemplateCode,
                    InvalidTemplateMessage,
                    GetLineNumber(element)));
                continue;
            }

            var monthElement = element.Element("Month")!;
            var amountElement = element.Element("Amount")!;
            var lineNumber = GetLineNumber(element);

            if (!TryParseMonth(monthElement.Value, out var month))
            {
                errors.Add(new(
                    "InvalidMonth",
                    "Ay değeri yyyy-AA biçiminde olmalıdır. Örnek: 2022-05.",
                    lineNumber));
                continue;
            }

            if (month < AssetImportRules.EarliestSupportedMonth)
            {
                errors.Add(new(
                    "MonthOutOfRange",
                    "Varlık verisi Aralık 2021 veya sonrasına ait olmalıdır.",
                    lineNumber));
                continue;
            }

            if (!decimal.TryParse(
                    amountElement.Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var amount))
            {
                errors.Add(new(
                    "InvalidAmount",
                    "Varlık tutarı nokta ondalık ayracı kullanan sayısal bir değer olmalıdır.",
                    lineNumber));
                continue;
            }

            if (amount < 0)
            {
                errors.Add(new(
                    "NegativeAmount",
                    "Varlık tutarı negatif olamaz.",
                    lineNumber));
                continue;
            }

            if (!seenMonths.Add(month))
            {
                errors.Add(new(
                    "DuplicateMonth",
                    $"{month:yyyy-MM} ayı dosyada birden fazla kez bulunuyor.",
                    lineNumber));
                continue;
            }

            values.Add(new MonthlyAssetValueInput(month, amount));
        }

        if (values.Count == 0 && errors.Count == 0)
        {
            errors.Add(new(
                "NoDataRows",
                "Varlık XML dosyasında işlenecek aylık kayıt bulunamadı."));
        }

        return new AssetFileParseResult(values, errors);
    }

    private static bool HasExpectedRecordShape(XElement element)
    {
        if (element.Name != "AssetValue" || element.HasAttributes)
        {
            return false;
        }

        var children = element.Elements().ToArray();

        return children.Length == 2 &&
               children.Count(child => child.Name == "Month" && !child.HasElements && !child.HasAttributes) == 1 &&
               children.Count(child => child.Name == "Amount" && !child.HasElements && !child.HasAttributes) == 1;
    }

    private static bool TryParseMonth(string value, out DateOnly month)
    {
        if (DateTime.TryParseExact(
                value.Trim(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            month = new DateOnly(parsed.Year, parsed.Month, 1);
            return true;
        }

        month = default;
        return false;
    }

    private static int? GetLineNumber(XObject node) =>
        node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? lineInfo.LineNumber
            : null;

    private static AssetFileParseResult InvalidTemplate() =>
        new([], [new AssetImportValidationError(InvalidTemplateCode, InvalidTemplateMessage)]);
}
