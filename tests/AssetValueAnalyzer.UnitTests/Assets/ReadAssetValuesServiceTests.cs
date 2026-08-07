using AssetValueAnalyzer.Application.Assets.Imports;

namespace AssetValueAnalyzer.UnitTests.Assets;

public sealed class ReadAssetValuesServiceTests
{
    [Fact]
    public async Task ReadAsync_WithValidParserResult_ReturnsNormalizedValues()
    {
        var parser = new StubAssetFileParser(new AssetFileParseResult(
            [
                new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_280_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_320_000m)
            ],
            []));
        var service = new ReadAssetValuesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1, 2, 3]),
            "varlik.xlsx",
            3,
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Values,
            value => Assert.Equal(new DateOnly(2021, 12, 1), value.Month),
            value => Assert.Equal(new DateOnly(2022, 1, 1), value.Month));
    }

    [Fact]
    public async Task ReadAsync_WhenParserFindsErrors_ReturnsErrors()
    {
        var parser = new StubAssetFileParser(new AssetFileParseResult(
            [],
            [new AssetImportValidationError("InvalidMonth", "Geçersiz tarih.", 2)]));
        var service = new ReadAssetValuesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1]),
            "varlik.xlsx",
            1,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("InvalidMonth", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ReadAsync_WhenParserReturnsNoRows_ReturnsValidationError()
    {
        var parser = new StubAssetFileParser(new AssetFileParseResult([], []));
        var service = new ReadAssetValuesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1]),
            "varlik.xlsx",
            1,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("NoDataRows", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ReadAsync_WithUnsupportedExtension_DoesNotCallParser()
    {
        var parser = new StubAssetFileParser(new AssetFileParseResult([], []));
        var service = new ReadAssetValuesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1]),
            "varlik.csv",
            1,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("UnsupportedFormat", Assert.Single(result.Errors).Code);
        Assert.Equal(0, parser.CallCount);
    }

    private sealed class StubAssetFileParser(AssetFileParseResult result)
        : IAssetFileParser
    {
        public int CallCount { get; private set; }

        public bool CanParse(string fileExtension) => fileExtension == ".xlsx";

        public Task<AssetFileParseResult> ParseAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
