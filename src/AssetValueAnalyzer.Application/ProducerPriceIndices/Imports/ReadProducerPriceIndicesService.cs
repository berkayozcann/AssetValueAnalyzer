namespace AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;

public sealed class ReadProducerPriceIndicesService(
    IEnumerable<IProducerPriceIndexFileParser> parsers)
{
    public const long MaxFileSize = 5 * 1024 * 1024;

    private readonly IReadOnlyList<IProducerPriceIndexFileParser> _parsers = parsers.ToArray();

    public async Task<ProducerPriceIndexFileParseResult> ReadAsync(
        Stream stream,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (fileSize <= 0)
        {
            return Invalid("EmptyFile", "Yİ-ÜFE Endeks Verisi dosyası boş olamaz.");
        }

        if (fileSize > MaxFileSize)
        {
            return Invalid("FileTooLarge", "Yİ-ÜFE Endeks Verisi dosyası en fazla 5 MB olabilir.");
        }

        var extension = Path.GetExtension(fileName);
        var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(extension));

        if (parser is null)
        {
            return Invalid(
                "UnsupportedFormat",
                "Yİ-ÜFE Endeks Verisi dosyası XLSX formatında olmalıdır.");
        }

        var result = await parser.ParseAsync(stream, cancellationToken);

        if (result.IsValid && result.Values.Count == 0)
        {
            return Invalid(
                "NoDataRows",
                "Yİ-ÜFE Endeks Verisi dosyasında işlenecek veri bulunamadı.");
        }

        return result;
    }

    private static ProducerPriceIndexFileParseResult Invalid(
        string code,
        string message) =>
        new([], [new ProducerPriceIndexImportValidationError(code, message)]);
}
