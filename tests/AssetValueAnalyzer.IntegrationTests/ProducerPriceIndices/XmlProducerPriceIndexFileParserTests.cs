using System.Text;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;

namespace AssetValueAnalyzer.IntegrationTests.ProducerPriceIndices;

public sealed class XmlProducerPriceIndexFileParserTests
{
    [Fact]
    public async Task ParseAsync_WithCanonicalXml_ReturnsMonthlyValues()
    {
        await using var stream = CreateStream(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <ProducerPriceIndices version="1.0">
              <ProducerPriceIndex><Month>2006-01</Month><IndexValue>122.38</IndexValue></ProducerPriceIndex>
              <ProducerPriceIndex><Month>2006-02</Month><IndexValue>123.84</IndexValue></ProducerPriceIndex>
            </ProducerPriceIndices>
            """);
        var parser = new XmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Values,
            value =>
            {
                Assert.Equal(new DateOnly(2006, 1, 1), value.Month);
                Assert.Equal(122.38m, value.Value);
            },
            value =>
            {
                Assert.Equal(new DateOnly(2006, 2, 1), value.Month);
                Assert.Equal(123.84m, value.Value);
            });
    }

    [Fact]
    public async Task ParseAsync_WithDuplicateMonth_ReturnsValidationError()
    {
        await using var stream = CreateStream(
            """
            <ProducerPriceIndices version="1.0">
              <ProducerPriceIndex><Month>2022-05</Month><IndexValue>100</IndexValue></ProducerPriceIndex>
              <ProducerPriceIndex><Month>2022-05</Month><IndexValue>200</IndexValue></ProducerPriceIndex>
            </ProducerPriceIndices>
            """);
        var parser = new XmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("DuplicateMonth", Assert.Single(result.Errors).Code);
        Assert.Single(result.Values);
    }

    [Fact]
    public async Task ParseAsync_WithMissingMonth_ReturnsValidationError()
    {
        await using var stream = CreateStream(
            """
            <ProducerPriceIndices version="1.0">
              <ProducerPriceIndex><Month>2022-01</Month><IndexValue>100</IndexValue></ProducerPriceIndex>
              <ProducerPriceIndex><Month>2022-03</Month><IndexValue>120</IndexValue></ProducerPriceIndex>
            </ProducerPriceIndices>
            """);
        var parser = new XmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == "MissingMonth" && error.Message.Contains("2022-02"));
    }

    [Theory]
    [InlineData("2022-13", "100", "InvalidMonth")]
    [InlineData("2022-05-20", "100", "InvalidMonth")]
    [InlineData("2022-05", "not-a-number", "InvalidIndexValue")]
    [InlineData("2022-05", "0", "NonPositiveIndexValue")]
    [InlineData("2022-05", "-1", "NonPositiveIndexValue")]
    public async Task ParseAsync_WithInvalidRecord_ReturnsExpectedError(
        string month,
        string value,
        string expectedCode)
    {
        await using var stream = CreateStream(
            $"""
             <ProducerPriceIndices version="1.0">
               <ProducerPriceIndex><Month>{month}</Month><IndexValue>{value}</IndexValue></ProducerPriceIndex>
             </ProducerPriceIndices>
             """);
        var parser = new XmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("<ProducerPriceIndices version=\"2.0\" />")]
    [InlineData("<WrongRoot version=\"1.0\" />")]
    [InlineData("<ProducerPriceIndices version=\"1.0\"><ProducerPriceIndex><Month>2022-05</Month></ProducerPriceIndex></ProducerPriceIndices>")]
    [InlineData("<ProducerPriceIndices version=\"1.0\"><ProducerPriceIndex><Month>2022-05</Month><IndexValue>100</IndexValue><Note>x</Note></ProducerPriceIndex></ProducerPriceIndices>")]
    [InlineData("not-xml")]
    public async Task ParseAsync_WithNonCanonicalDocument_ReturnsTemplateError(string xml)
    {
        await using var stream = CreateStream(xml);
        var parser = new XmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        AssertInvalidTemplate(result);
    }

    [Fact]
    public async Task ParseAsync_WithDtd_ReturnsTemplateErrorWithoutResolvingEntity()
    {
        await using var stream = CreateStream(
            """
            <!DOCTYPE ProducerPriceIndices [<!ENTITY external SYSTEM "file:///etc/passwd">]>
            <ProducerPriceIndices version="1.0">
              <ProducerPriceIndex><Month>2022-05</Month><IndexValue>&external;</IndexValue></ProducerPriceIndex>
            </ProducerPriceIndices>
            """);
        var parser = new XmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        AssertInvalidTemplate(result);
    }

    private static MemoryStream CreateStream(string xml) =>
        new(Encoding.UTF8.GetBytes(xml));

    private static void AssertInvalidTemplate(ProducerPriceIndexFileParseResult result)
    {
        Assert.False(result.IsValid);
        Assert.Equal(
            "InvalidProducerPriceIndexTemplate",
            Assert.Single(result.Errors).Code);
        Assert.Empty(result.Values);
    }
}
