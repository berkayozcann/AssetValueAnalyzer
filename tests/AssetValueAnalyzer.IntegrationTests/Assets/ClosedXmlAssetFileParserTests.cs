using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.IntegrationTests.Assets;

public sealed class ClosedXmlAssetFileParserTests
{
    [Fact]
    public async Task ParseAsync_WithDownloadableSample_ReturnsExpectedMonthlyValues()
    {
        var samplePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../..",
            "src/AssetValueAnalyzer.Web/wwwroot/samples/asset-values.xlsx"));
        await using var stream = File.OpenRead(samplePath);
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(22, result.Values.Count);
        Assert.Equal(new DateOnly(2021, 12, 1), result.Values[0].Month);
        Assert.Equal(1_280_000m, result.Values[0].Amount);
        Assert.Equal(new DateOnly(2023, 9, 1), result.Values[^1].Month);
        Assert.Equal(2_120_000m, result.Values[^1].Amount);
    }

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
    public async Task ParseAsync_WithAnyDayOfMonth_NormalizesValueToFirstDayOfMonth()
    {
        await using var stream = CreateWorkbook(
            (new DateTime(2022, 5, 27), 1_500_000m));
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2022, 5, 1), value.Month);
        Assert.Equal(1_500_000m, value.Amount);
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
    public async Task ParseAsync_WithArbitraryWorksheetNameAndExpectedHeaders_ReturnsMonthlyValues()
    {
        await using var stream = CreateWorkbook(
            "Şirket Verileri 2023",
            includeHeaders: true,
            (new DateTime(2021, 12, 1), 1_280_000m));
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
        Assert.Equal(1_280_000m, value.Amount);
    }

    [Fact]
    public async Task ParseAsync_WithoutHeaders_ReturnsFirstRowAsMonthlyValue()
    {
        await using var stream = CreateWorkbook(
            "İstediğim Sekme Adı",
            includeHeaders: false,
            (new DateTime(2021, 12, 1), 1_280_000m),
            (new DateTime(2022, 1, 1), 1_320_000m));
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Values.Count);
        Assert.Equal(new DateOnly(2021, 12, 1), result.Values[0].Month);
        Assert.Equal(1_280_000m, result.Values[0].Amount);
    }

    [Fact]
    public async Task ParseAsync_WithEmptyWorksheet_ReturnsGenericTemplateError()
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
    public async Task ParseAsync_WithArbitraryHeadersAndValidRows_ReturnsMonthlyValues()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Varlık Tablosu");
            worksheet.Cell(1, 1).Value = "Ay";
            worksheet.Cell(1, 2).Value = "Şirket Değeri";
            worksheet.Cell(2, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(2, 2).Value = 1_280_000m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
        Assert.Equal(1_280_000m, value.Amount);
    }

    [Fact]
    public async Task ParseAsync_WithMultipleHeadingRows_SkipsRowsUntilMonthlyDataStarts()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Serbest Sekme Adı");
            worksheet.Cell(1, 1).Value = "Şirket Finansal Varlık Raporu";
            worksheet.Cell(2, 1).Value = "Hazırlanma tarihi: 07.08.2026";
            worksheet.Cell(3, 1).Value = "Ay";
            worksheet.Cell(3, 2).Value = "Tutar";
            worksheet.Cell(4, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(4, 2).Value = 1_280_000m;
            worksheet.Cell(5, 1).Value = new DateTime(2022, 1, 1);
            worksheet.Cell(5, 2).Value = 1_320_000m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Values.Count);
        Assert.Equal(new DateOnly(2021, 12, 1), result.Values[0].Month);
        Assert.Equal(1_280_000m, result.Values[0].Amount);
    }

    [Fact]
    public async Task ParseAsync_WithExcelDateSerialAndDateFormat_ReturnsMonthlyValue()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Serbest Sekme Adı");
            worksheet.Cell(1, 1).Value = "İstenilen başlık";
            worksheet.Cell(1, 2).Value = "İstenilen tutar başlığı";
            worksheet.Cell(2, 1).Value = 44531d;
            worksheet.Cell(2, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            worksheet.Cell(2, 2).Value = 1_280_000m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
        Assert.Equal(1_280_000m, value.Amount);
    }

    [Fact]
    public async Task ParseAsync_WithMonthBeforeSupportedRange_ReturnsRowError()
    {
        await using var stream = CreateWorkbook(
            (new DateTime(2021, 11, 1), 1_200_000m),
            (new DateTime(2021, 12, 1), 1_280_000m));
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("MonthOutOfRange", error.Code);
        Assert.Equal(2, error.RowNumber);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
    }

    [Fact]
    public async Task ParseAsync_WithThirdDataColumn_ReturnsRowError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Serbest Sekme Adı");
            worksheet.Cell(1, 1).Value = "Ay";
            worksheet.Cell(1, 2).Value = "Tutar";
            worksheet.Cell(1, 3).Value = "Açıklama";
            worksheet.Cell(2, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(2, 2).Value = 1_280_000m;
            worksheet.Cell(2, 3).Value = "Fazladan veri";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("UnexpectedColumns", error.Code);
        Assert.Equal(2, error.RowNumber);
        Assert.Empty(result.Values);
    }

    [Fact]
    public async Task ParseAsync_WithTextAmountAfterDataStarts_ReturnsRowError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Serbest Sekme Adı");
            worksheet.Cell(1, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(1, 2).Value = 1_280_000m;
            worksheet.Cell(2, 1).Value = new DateTime(2022, 1, 1);
            worksheet.Cell(2, 2).Value = "1320000";
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("InvalidAmount", error.Code);
        Assert.Equal(2, error.RowNumber);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
    }

    [Fact]
    public async Task ParseAsync_WithTextAmountInFirstDataRow_ReturnsRowError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Serbest Sekme Adı");
            worksheet.Cell(1, 1).Value = "İstenilen başlık";
            worksheet.Cell(1, 2).Value = "İstenilen tutar başlığı";
            worksheet.Cell(2, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(2, 2).Value = "1280000";
            worksheet.Cell(3, 1).Value = new DateTime(2022, 1, 1);
            worksheet.Cell(3, 2).Value = 1_320_000m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("InvalidAmount", error.Code);
        Assert.Equal(2, error.RowNumber);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2022, 1, 1), value.Month);
    }

    [Fact]
    public async Task ParseAsync_WithUnformattedNumericDateAfterDataStarts_ReturnsRowError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Serbest Sekme Adı");
            worksheet.Cell(1, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(1, 2).Value = 1_280_000m;
            worksheet.Cell(2, 1).Value = 44562d;
            worksheet.Cell(2, 2).Value = 1_320_000m;
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        var parser = new ClosedXmlAssetFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("InvalidMonth", error.Code);
        Assert.Equal(2, error.RowNumber);
        var value = Assert.Single(result.Values);
        Assert.Equal(new DateOnly(2021, 12, 1), value.Month);
    }

    [Fact]
    public async Task ParseAsync_WithIndexStyleNumericYearRows_ReturnsGenericTemplateError()
    {
        await using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Endeks");
            worksheet.Cell(1, 1).Value = "Yİ-ÜFE Endeks Tablosu";
            worksheet.Cell(6, 1).Value = "Yıl";
            worksheet.Cell(6, 2).Value = "Ocak";
            worksheet.Cell(7, 1).Value = 2021;
            worksheet.Cell(7, 2).Value = 686.95m;
            worksheet.Cell(8, 1).Value = 2022;
            worksheet.Cell(8, 2).Value = 1290.24m;
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
        => CreateWorkbook("Varlık Tablosu", includeHeaders: true, rows);

    private static MemoryStream CreateWorkbook(
        string worksheetName,
        bool includeHeaders,
        params (DateTime Month, decimal Amount)[] rows)
    {
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add(worksheetName);
            var firstDataRow = includeHeaders ? 2 : 1;

            if (includeHeaders)
            {
                worksheet.Cell(1, 1).Value = "Tarih";
                worksheet.Cell(1, 2).Value = "Varlık Tutarı";
            }

            for (var index = 0; index < rows.Length; index++)
            {
                worksheet.Cell(index + firstDataRow, 1).Value = rows[index].Month;
                worksheet.Cell(index + firstDataRow, 2).Value = rows[index].Amount;
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
