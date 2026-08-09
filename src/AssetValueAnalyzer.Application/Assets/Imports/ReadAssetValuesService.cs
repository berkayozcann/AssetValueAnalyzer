namespace AssetValueAnalyzer.Application.Assets.Imports;

public sealed class ReadAssetValuesService(IEnumerable<IAssetFileParser> parsers)
{
    public const long MaxFileSize = 5 * 1024 * 1024;

    private readonly IReadOnlyList<IAssetFileParser> _parsers = parsers.ToArray();

    public async Task<AssetFileParseResult> ReadAsync(
        Stream stream,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (fileSize <= 0)
        {
            return Invalid("EmptyFile", "Varlık dosyası boş olamaz.");
        }

        if (fileSize > MaxFileSize)
        {
            return Invalid("FileTooLarge", "Varlık dosyası en fazla 5 MB olabilir.");
        }

        var extension = Path.GetExtension(fileName);
        var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(extension));

        if (parser is null)
        {
            return Invalid(
                "UnsupportedFormat",
                "Varlık dosyası XLSX formatında olmalıdır.");
        }

        var result = await parser.ParseAsync(stream, cancellationToken);

        if (result.IsValid && result.Values.Count == 0)
        {
            return Invalid(
                "NoDataRows",
                "Varlık dosyasında işlenecek veri satırı bulunamadı.");
        }

        return result;
    }

    private static AssetFileParseResult Invalid(string code, string message) =>
        new([], [new AssetImportValidationError(code, message)]);
}
