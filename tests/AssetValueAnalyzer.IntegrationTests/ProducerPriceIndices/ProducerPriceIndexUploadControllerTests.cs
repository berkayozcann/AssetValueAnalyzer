using System.Text;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.Web.Controllers;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.IntegrationTests.ProducerPriceIndices;

public sealed class ProducerPriceIndexUploadControllerTests
{
    [Fact]
    public async Task UploadProducerPriceIndices_WithValidXml_ReturnsValidationSummary()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                """
                <ProducerPriceIndices version="1.0">
                  <ProducerPriceIndex><Month>2006-01</Month><IndexValue>122.38</IndexValue></ProducerPriceIndex>
                  <ProducerPriceIndex><Month>2006-02</Month><IndexValue>123.84</IndexValue></ProducerPriceIndex>
                </ProducerPriceIndices>
                """));
        var controller = CreateController();
        var file = new FormFile(stream, 0, stream.Length, "file", "serbest-ad.xml");

        var result = await controller.UploadProducerPriceIndices(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProducerPriceIndexFileValidationResult>(ok.Value);
        Assert.True(response.IsValid);
        Assert.Equal(2, response.ParsedCount);
        Assert.Equal(new DateOnly(2006, 1, 1), response.FirstMonth);
        Assert.Equal(new DateOnly(2006, 2, 1), response.LastMonth);
    }

    [Fact]
    public async Task UploadProducerPriceIndices_WithValidXlsx_ReturnsValidationSummary()
    {
        await using var stream = CreateIndexWorkbook();
        var controller = CreateController();
        var file = new FormFile(stream, 0, stream.Length, "file", "endeks.xlsx");

        var result = await controller.UploadProducerPriceIndices(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ProducerPriceIndexFileValidationResult>(ok.Value);
        Assert.True(response.IsValid);
        Assert.Equal(2, response.ParsedCount);
        Assert.Equal(new DateOnly(2006, 1, 1), response.FirstMonth);
        Assert.Equal(new DateOnly(2006, 2, 1), response.LastMonth);
    }

    [Fact]
    public async Task UploadProducerPriceIndices_WithWrongXlsxTemplate_ReturnsUnprocessableEntity()
    {
        await using var stream = CreateWrongWorkbook();
        var controller = CreateController();
        var file = new FormFile(stream, 0, stream.Length, "file", "yanlis.xlsx");

        var result = await controller.UploadProducerPriceIndices(file, CancellationToken.None);

        var invalid = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var response = Assert.IsType<ProducerPriceIndexFileValidationResult>(invalid.Value);
        Assert.False(response.IsValid);
        Assert.Equal("InvalidProducerPriceIndexTemplate", Assert.Single(response.Errors).Code);
    }

    private static ImportsController CreateController() =>
        new(
            new ReadAssetValuesService([new ClosedXmlAssetFileParser()]),
            new ReadProducerPriceIndicesService(
                [new ClosedXmlProducerPriceIndexFileParser(), new XmlProducerPriceIndexFileParser()]));

    private static MemoryStream CreateIndexWorkbook()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("İstenen herhangi bir sekme adı");
        string[] headers =
        [
            "Yıl", "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
            "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
        ];

        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(4, index + 1).Value = headers[index];
        }

        worksheet.Cell(7, 1).Value = 2006;
        worksheet.Cell(7, 2).Value = 122.38m;
        worksheet.Cell(7, 3).Value = 123.84m;

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateWrongWorkbook()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Yanlış");
        worksheet.Cell(1, 1).Value = "Tarih";
        worksheet.Cell(1, 2).Value = "Varlık Tutarı";
        worksheet.Cell(2, 1).Value = new DateTime(2021, 12, 1);
        worksheet.Cell(2, 2).Value = 1_000_000m;

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
