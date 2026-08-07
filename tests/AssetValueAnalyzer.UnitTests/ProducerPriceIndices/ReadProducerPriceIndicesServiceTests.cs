using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;

namespace AssetValueAnalyzer.UnitTests.ProducerPriceIndices;

public sealed class ReadProducerPriceIndicesServiceTests
{
    [Fact]
    public async Task ReadAsync_WithValidParserResult_ReturnsNormalizedValues()
    {
        var parser = new StubParser(new ProducerPriceIndexFileParseResult(
            [
                new MonthlyProducerPriceIndexInput(new DateOnly(2021, 12, 1), 1022.25m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 1129.03m)
            ],
            []));
        var service = new ReadProducerPriceIndicesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1, 2, 3]),
            "endeks.xlsx",
            3,
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Values.Count);
    }

    [Theory]
    [InlineData("", 0, "EmptyFile")]
    [InlineData("endeks.csv", 1, "UnsupportedFormat")]
    public async Task ReadAsync_WithInvalidFileMetadata_DoesNotCallParser(
        string fileName,
        long fileSize,
        string expectedCode)
    {
        var parser = new StubParser(new ProducerPriceIndexFileParseResult([], []));
        var service = new ReadProducerPriceIndicesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1]),
            fileName,
            fileSize,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, Assert.Single(result.Errors).Code);
        Assert.Equal(0, parser.CallCount);
    }

    [Fact]
    public async Task ReadAsync_WithFileLargerThanLimit_DoesNotCallParser()
    {
        var parser = new StubParser(new ProducerPriceIndexFileParseResult([], []));
        var service = new ReadProducerPriceIndicesService([parser]);

        var result = await service.ReadAsync(
            new MemoryStream([1]),
            "endeks.xlsx",
            ReadProducerPriceIndicesService.MaxFileSize + 1,
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("FileTooLarge", Assert.Single(result.Errors).Code);
        Assert.Equal(0, parser.CallCount);
    }

    private sealed class StubParser(ProducerPriceIndexFileParseResult result)
        : IProducerPriceIndexFileParser
    {
        public int CallCount { get; private set; }

        public bool CanParse(string fileExtension) => fileExtension == ".xlsx";

        public Task<ProducerPriceIndexFileParseResult> ParseAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}
