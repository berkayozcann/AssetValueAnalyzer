namespace AssetValueAnalyzer.Application.Assets.Imports;

public interface IAssetFileParser
{
    bool CanParse(string fileExtension);

    Task<AssetFileParseResult> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
