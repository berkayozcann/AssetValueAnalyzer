namespace AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;

public interface IProducerPriceIndexFileParser
{
    bool CanParse(string fileExtension);

    Task<ProducerPriceIndexFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
