using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;

namespace AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;

public sealed class XmlProducerPriceIndexFileParser
    : IProducerPriceIndexFileParser
{
    private const string InvalidTemplateCode = "InvalidProducerPriceIndexTemplate";
    private const string InvalidTemplateMessage =
        "Dosya beklenen Endeks Verisi XML şablonuna uygun değildir. Lütfen örnek dosyayı kontrol edip yeniden deneyin.";

    public bool CanParse(string fileExtension) =>
        string.Equals(fileExtension, ".xml", StringComparison.OrdinalIgnoreCase);

    public async Task<ProducerPriceIndexFileParseResult> ParseAsync(
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
            MaxCharactersInDocument = ReadProducerPriceIndicesService.MaxFileSize
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

    private static ProducerPriceIndexFileParseResult ParseDocument(XDocument document)
    {
        var root = document.Root;

        if (root is null ||
            root.Name != "ProducerPriceIndices" ||
            root.Attribute("version")?.Value != "1.0" ||
            root.Attributes().Any(attribute => attribute.Name != "version"))
        {
            return InvalidTemplate();
        }

        var values = new List<MonthlyProducerPriceIndexInput>();
        var errors = new List<ProducerPriceIndexImportValidationError>();
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
            var indexValueElement = element.Element("IndexValue")!;
            var lineNumber = GetLineNumber(element);

            if (!TryParseMonth(monthElement.Value, out var month))
            {
                errors.Add(new(
                    "InvalidMonth",
                    "Ay değeri yyyy-AA biçiminde olmalıdır. Örnek: 2022-05.",
                    lineNumber));
                continue;
            }

            if (!decimal.TryParse(
                    indexValueElement.Value,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var indexValue))
            {
                errors.Add(new(
                    "InvalidIndexValue",
                    "Endeks değeri nokta ondalık ayracı kullanan sayısal bir değer olmalıdır.",
                    lineNumber));
                continue;
            }

            if (indexValue <= 0)
            {
                errors.Add(new(
                    "NonPositiveIndexValue",
                    "Endeks değeri sıfırdan büyük olmalıdır.",
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

            values.Add(new MonthlyProducerPriceIndexInput(month, indexValue));
        }

        if (values.Count == 0 && errors.Count == 0)
        {
            errors.Add(new(
                "NoDataRows",
                "Endeks XML dosyasında işlenecek aylık kayıt bulunamadı."));
        }

        AddMissingMonthErrors(values, errors);

        return new ProducerPriceIndexFileParseResult(
            values.OrderBy(value => value.Month).ToArray(),
            errors);
    }

    private static bool HasExpectedRecordShape(XElement element)
    {
        if (element.Name != "ProducerPriceIndex" || element.HasAttributes)
        {
            return false;
        }

        var children = element.Elements().ToArray();

        return children.Length == 2 &&
               children.Count(child => child.Name == "Month" && !child.HasElements && !child.HasAttributes) == 1 &&
               children.Count(child => child.Name == "IndexValue" && !child.HasElements && !child.HasAttributes) == 1;
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

    private static void AddMissingMonthErrors(
        IReadOnlyCollection<MonthlyProducerPriceIndexInput> values,
        ICollection<ProducerPriceIndexImportValidationError> errors)
    {
        if (values.Count == 0)
        {
            return;
        }

        var presentMonths = values.Select(value => value.Month).ToHashSet();
        var firstMonth = values.Min(value => value.Month);
        var lastMonth = values.Max(value => value.Month);

        for (var month = firstMonth;
             month <= lastMonth;
             month = month.AddMonths(1))
        {
            if (!presentMonths.Contains(month))
            {
                errors.Add(new(
                    "MissingMonth",
                    $"{month:yyyy-MM} ayına ait endeks değeri bulunamadı."));
            }
        }
    }

    private static int? GetLineNumber(XObject node) =>
        node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? lineInfo.LineNumber
            : null;

    private static ProducerPriceIndexFileParseResult InvalidTemplate() =>
        new([], [new ProducerPriceIndexImportValidationError(
            InvalidTemplateCode,
            InvalidTemplateMessage)]);
}
