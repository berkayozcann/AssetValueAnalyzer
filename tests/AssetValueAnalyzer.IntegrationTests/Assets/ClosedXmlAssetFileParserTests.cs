using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.IntegrationTests.Assets;

public sealed class ClosedXmlAssetFileParserTests
{
    [Fact]
    public async Task ParseAsync_WithExpectedTemplate_ReturnsMonthlyValues()
    {
        await using var stream = CreateWorkbook(
            (new DateTime(2021, 12, 1), 1_280_000m),
            (new DateTime(2022, 1, 1), 1_320_000m));
        var parser = new ClosedXmlAssetFileParser();

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
                Assert.Equal(1_320_000m, value.Amount);
            });
    }

    [Fact]
    public async Task ParseAsync_WithDuplicateMonth_ReturnsRowError()
    {
        await using var stream = CreateWorkbook(
            (new DateTime(2022, 1, 1), 1_320_000m),
            (new DateTime(2022, 1, 20), 1_340_000m));
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("DuplicateMonth", error.Code);
        Assert.Equal(3, error.RowNumber);
    }

    [Fact]
    public async Task ParseAsync_WithDifferentWorksheet_ReturnsGenericTemplateError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("Sayfa1");
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        AssertInvalidAssetTemplate(result);
    }

    [Fact]
    public async Task ParseAsync_WithUnexpectedHeaders_ReturnsGenericTemplateError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Varlık Tablosu");
            worksheet.Cell(1, 1).Value = "Beklenmeyen kolon";
            worksheet.Cell(1, 2).Value = "Başka kolon";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        AssertInvalidAssetTemplate(result);
    }

    [Fact]
    public async Task ParseAsync_WithNonXlsxContent_ReturnsGenericTemplateError()
    {
        await using var stream = new MemoryStream("not-an-xlsx"u8.ToArray());
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        AssertInvalidAssetTemplate(result);
    }

    private static MemoryStream CreateWorkbook(
        params (DateTime Month, decimal Amount)[] rows)
    {
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Varlık Tablosu");
            worksheet.Cell(1, 1).Value = "Tarih";
            worksheet.Cell(1, 2).Value = "Varlık Tutarı";

            for (var index = 0; index < rows.Length; index++)
            {
                worksheet.Cell(index + 2, 1).Value = rows[index].Month;
                worksheet.Cell(index + 2, 2).Value = rows[index].Amount;
            }

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private static void AssertInvalidAssetTemplate(AssetFileParseResult result)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal("InvalidAssetTemplate", error.Code);
        Assert.Equal(
            "Dosya beklenen Varlık Verisi şablonuna uygun değildir. Lütfen örnek dosyayı kontrol edip yeniden deneyin.",
            error.Message);
    }
}
