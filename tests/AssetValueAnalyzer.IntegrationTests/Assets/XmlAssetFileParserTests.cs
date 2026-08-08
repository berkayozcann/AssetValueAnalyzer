using System.Text;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;

namespace AssetValueAnalyzer.IntegrationTests.Assets;

public sealed class XmlAssetFileParserTests
{
    [Fact]
    public async Task ParseAsync_WithCanonicalXml_ReturnsMonthlyValues()
    {
        await using var stream = CreateStream(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <AssetValues version="1.0">
              <AssetValue>
                <Month>2021-12</Month>
                <Amount>1280000.00</Amount>
              </AssetValue>
              <AssetValue>
                <Month>2022-01</Month>
                <Amount>1320000.50</Amount>
              </AssetValue>
            </AssetValues>
            """);
        var parser = new XmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Values,
            value =>
            {
                Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
                Assert.Equal(1_280_000m, value.Amount);
            },
            value =>
            {
                Assert.Equal(new DateOnly(2022, 1, 1), value.Month);
                Assert.Equal(1_320_000.50m, value.Amount);
            });
    }

    [Fact]
    public async Task ParseAsync_WithDuplicateMonth_ReturnsValidationError()
    {
        await using var stream = CreateStream(
            """
            <AssetValues version="1.0">
              <AssetValue><Month>2022-05</Month><Amount>100</Amount></AssetValue>
              <AssetValue><Month>2022-05</Month><Amount>200</Amount></AssetValue>
            </AssetValues>
            """);
        var parser = new XmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("DuplicateMonth", Assert.Single(result.Errors).Code);
        Assert.Single(result.Values);
    }

    [Theory]
    [InlineData("2021-11", "100", "MonthOutOfRange")]
    [InlineData("2022-13", "100", "InvalidMonth")]
    [InlineData("2022-05-20", "100", "InvalidMonth")]
    [InlineData("2022-05", "not-a-number", "InvalidAmount")]
    [InlineData("2022-05", "-1", "NegativeAmount")]
    public async Task ParseAsync_WithInvalidRecord_ReturnsExpectedError(
        string month,
        string amount,
        string expectedCode)
    {
        await using var stream = CreateStream(
            $"""
             <AssetValues version="1.0">
               <AssetValue><Month>{month}</Month><Amount>{amount}</Amount></AssetValue>
             </AssetValues>
             """);
        var parser = new XmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("<AssetValues version=\"2.0\" />")]
    [InlineData("<WrongRoot version=\"1.0\" />")]
    [InlineData("<AssetValues version=\"1.0\"><AssetValue><Month>2022-05</Month></AssetValue></AssetValues>")]
    [InlineData("<AssetValues version=\"1.0\"><AssetValue><Month>2022-05</Month><Amount>100</Amount><Note>x</Note></AssetValue></AssetValues>")]
    [InlineData("not-xml")]
    public async Task ParseAsync_WithNonCanonicalDocument_ReturnsTemplateError(string xml)
    {
        await using var stream = CreateStream(xml);
        var parser = new XmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        AssertInvalidTemplate(result);
    }

    [Fact]
    public async Task ParseAsync_WithDtd_ReturnsTemplateErrorWithoutResolvingEntity()
    {
        await using var stream = CreateStream(
            """
            <!DOCTYPE AssetValues [<!ENTITY external SYSTEM "file:///etc/passwd">]>
            <AssetValues version="1.0">
              <AssetValue><Month>2022-05</Month><Amount>&external;</Amount></AssetValue>
            </AssetValues>
            """);
        var parser = new XmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        AssertInvalidTemplate(result);
    }

    private static MemoryStream CreateStream(string xml) =>
        new(Encoding.UTF8.GetBytes(xml));

    private static void AssertInvalidTemplate(AssetFileParseResult result)
    {
        Assert.False(result.IsValid);
        Assert.Equal("InvalidAssetTemplate", Assert.Single(result.Errors).Code);
        Assert.Empty(result.Values);
    }
}
