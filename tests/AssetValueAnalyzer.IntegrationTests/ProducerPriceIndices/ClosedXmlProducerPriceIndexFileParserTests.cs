using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using ClosedXML.Excel;

namespace AssetValueAnalyzer.IntegrationTests.ProducerPriceIndices;

public sealed class ClosedXmlProducerPriceIndexFileParserTests
{
    [Fact]
    public async Task ParseAsync_WithPublishedSample_ReturnsExpectedCompanyValues()
    {
        var samplePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../",
            "src/AssetValueAnalyzer.Web/wwwroot/samples/producer-price-indices.xlsx"));
        await using var stream = File.OpenRead(samplePath);
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(212, result.Values.Count);
        Assert.Equal(new DateOnly(2006, 1, 1), result.Values[0].Month);
        Assert.Equal(123.513548041274m, result.Values[0].Value);
        Assert.Contains(result.Values, value =>
            value.Month == new DateOnly(2021, 12, 1) &&
            value.Value == 1022.25m);
        Assert.Equal(new DateOnly(2023, 8, 1), result.Values[^1].Month);
        Assert.Equal(2602.54m, result.Values[^1].Value);
    }

    [Fact]
    public async Task ParseAsync_WithCompanyShapedMatrix_ReturnsAllAvailableMonths()
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            AddHeaders(worksheet, 4);

            var rowNumber = 7;

            for (var year = 2006; year <= 2022; year++)
            {
                var values = Enumerable.Range(1, 12)
                    .Select(month => (decimal?)(year * 100m + month))
                    .ToArray();

                if (year == 2021)
                {
                    values[11] = 1022.25m;
                }

                AddYear(worksheet, rowNumber++, year, values);
            }

            AddYear(
                worksheet,
                rowNumber,
                2023,
                [1900m, 2000m, 2100m, 2200m, 2300m, 2400m, 2500m, 2602.54m]);
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(212, result.Values.Count);
        Assert.Equal(new DateOnly(2006, 1, 1), result.Values[0].Month);
        Assert.Equal(200601m, result.Values[0].Value);
        Assert.Contains(result.Values, value =>
            value.Month == new DateOnly(2021, 12, 1) &&
            value.Value == 1022.25m);
        Assert.Equal(new DateOnly(2023, 8, 1), result.Values[^1].Month);
        Assert.Equal(2602.54m, result.Values[^1].Value);
    }

    [Fact]
    public async Task ParseAsync_WithArbitrarySheetNameAndTitleRows_IsAccepted()
    {
        await using var stream = CreateWorkbook(
            worksheet =>
            {
                AddHeaders(worksheet, 8);
                AddYear(worksheet, 10, 2021, [null, null, null, null, null, null, null, null, null, null, null, 1022.25m]);
            },
            "Firmanın İstediği Sekme Adı");
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Single(result.Values);
        Assert.Equal(1022.25m, result.Values[0].Value);
    }

    [Fact]
    public async Task ParseAsync_WithBlankMonthBeforeLastValue_ReturnsAvailableMonths()
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            AddHeaders(worksheet, 1);
            AddYear(worksheet, 2, 2021, [null, null, null, null, null, null, null, null, null, null, null, 1022.25m]);
            AddYear(worksheet, 3, 2022, [1129.03m, null, 1300m]);
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Collection(
            result.Values,
            value => Assert.Equal(new DateOnly(2021, 12, 1), value.Month),
            value => Assert.Equal(new DateOnly(2022, 1, 1), value.Month),
            value => Assert.Equal(new DateOnly(2022, 3, 1), value.Month));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ParseAsync_WithNonPositiveIndex_ReturnsValidationError(decimal value)
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            AddHeaders(worksheet, 1);
            AddYear(worksheet, 2, 2021, [null, null, null, null, null, null, null, null, null, null, null, value]);
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("NonPositiveIndexValue", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ParseAsync_WithWrongTemplate_ReturnsFriendlyTemplateError()
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            worksheet.Cell(1, 1).Value = "Tarih";
            worksheet.Cell(1, 2).Value = "Varlık Tutarı";
            worksheet.Cell(2, 1).Value = new DateTime(2021, 12, 1);
            worksheet.Cell(2, 2).Value = 1_000_000m;
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("InvalidProducerPriceIndexTemplate", error.Code);
        Assert.Contains("örnek dosyayı", error.Message);
    }

    [Fact]
    public async Task ParseAsync_WithoutYearHeader_ReturnsFriendlyTemplateError()
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            AddHeaders(worksheet, 4);
            worksheet.Cell(4, 1).Value = "Dönem";
            AddYear(worksheet, 7, 2022, Enumerable.Repeat<decimal?>(100m, 12).ToArray());
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("InvalidProducerPriceIndexTemplate", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task ParseAsync_WithDuplicateYear_ReturnsDuplicateYearError()
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            AddHeaders(worksheet, 4);
            AddYear(worksheet, 7, 2022, Enumerable.Repeat<decimal?>(100m, 12).ToArray());
            AddYear(worksheet, 8, 2022, Enumerable.Repeat<decimal?>(200m, 12).ToArray());
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "DuplicateYear");
    }

    [Fact]
    public async Task ParseAsync_WithUnexpectedDataColumn_ReturnsUnexpectedColumnsError()
    {
        await using var stream = CreateWorkbook(worksheet =>
        {
            AddHeaders(worksheet, 4);
            AddYear(worksheet, 7, 2022, Enumerable.Repeat<decimal?>(100m, 12).ToArray());
            worksheet.Cell(7, 14).Value = "Fazladan veri";
        });
        var parser = new ClosedXmlProducerPriceIndexFileParser();

        var result = await parser.ParseAsync(stream, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("UnexpectedColumns", Assert.Single(result.Errors).Code);
    }

    private static MemoryStream CreateWorkbook(
        Action<IXLWorksheet> configure,
        string worksheetName = "Sayfa1")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet(worksheetName);
        configure(worksheet);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void AddHeaders(IXLWorksheet worksheet, int rowNumber)
    {
        string[] headers =
        [
            "Yıl", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        ];

        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(rowNumber, index + 1).Value = headers[index];
        }
    }

    private static void AddYear(
        IXLWorksheet worksheet,
        int rowNumber,
        int year,
        IReadOnlyList<decimal?> values)
    {
        worksheet.Cell(rowNumber, 1).Value = year;

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index] is { } value)
            {
                worksheet.Cell(rowNumber, index + 2).Value = value;
            }
        }
    }
}
