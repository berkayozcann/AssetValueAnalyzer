using System.Text;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.IntegrationTests.Assets;

public sealed class AssetUploadControllerTests
{
    [Fact]
    public async Task UploadAssets_WithValidXml_ReturnsValidationSummary()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                """
                <AssetValues version="1.0">
                  <AssetValue><Month>2021-12</Month><Amount>1000000.00</Amount></AssetValue>
                  <AssetValue><Month>2022-01</Month><Amount>1050000.00</Amount></AssetValue>
                </AssetValues>
                """));
        var controller = CreateController();
        var file = new FormFile(stream, 0, stream.Length, "file", "serbest-ad.xml");

        var result = await controller.UploadAssets(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AssetFileValidationResult>(ok.Value);
        Assert.True(response.IsValid);
        Assert.Equal(2, response.ParsedCount);
        Assert.Equal(new DateOnly(2021, 12, 1), response.FirstMonth);
        Assert.Equal(new DateOnly(2022, 1, 1), response.LastMonth);
    }

    private static ImportsController CreateController() =>
        new(
            new ReadAssetValuesService(
                [new ClosedXmlAssetFileParser(), new XmlAssetFileParser()]),
            new ReadProducerPriceIndicesService(
                [new ClosedXmlProducerPriceIndexFileParser(), new XmlProducerPriceIndexFileParser()]));
}
