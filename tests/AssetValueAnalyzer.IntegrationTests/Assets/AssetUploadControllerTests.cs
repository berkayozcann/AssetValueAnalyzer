using System.Text;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Reports;
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
        var workspace = new TestReportWorkspaceSession();
        var controller = CreateController(workspace);
        var file = new FormFile(stream, 0, stream.Length, "file", "serbest-ad.xml");

        var result = await controller.UploadAssets(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AssetFileValidationResult>(ok.Value);
        Assert.True(response.IsValid);
        Assert.Equal(2, response.ParsedCount);
        Assert.Equal(new DateOnly(2021, 12, 1), response.FirstMonth);
        Assert.Equal(new DateOnly(2022, 1, 1), response.LastMonth);
        var storedFile = Assert.IsType<ReportDataFileSnapshot>(workspace.Get().AssetValues);
        Assert.Equal("serbest-ad.xml", storedFile.FileName);
        Assert.Equal(2, storedFile.ParsedCount);
    }

    [Fact]
    public void ClearAssets_RemovesValidatedAssetFileFromWorkspace()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000m)]);
        var controller = CreateController(workspace);

        var result = controller.ClearAssets();

        Assert.IsType<NoContentResult>(result);
        Assert.Null(workspace.Get().AssetValues);
    }

    [Fact]
    public async Task UploadAssets_WithCompletedReport_ReturnsConflictWithoutReplacingWorkspace()
    {
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "<AssetValues version=\"1.0\"><AssetValue><Month>2021-12</Month><Amount>1000</Amount></AssetValue></AssetValues>"));
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveCompletedReport(TestReportPageViewModelFactory.Create());
        var controller = CreateController(workspace);
        var file = new FormFile(stream, 0, stream.Length, "file", "yeni.xml");

        var result = await controller.UploadAssets(file, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<AssetFileValidationResult>(conflict.Value);
        Assert.Equal("CompletedReportLocked", Assert.Single(response.Errors).Code);
        Assert.NotNull(workspace.Get().CompletedReport);
    }

    [Fact]
    public async Task UploadAssets_WithExistingFile_ReplacesPreviousSnapshot()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "eski.xlsx",
            [new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000m)]);
        await using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "<AssetValues version=\"1.0\"><AssetValue><Month>2022-01</Month><Amount>2000</Amount></AssetValue></AssetValues>"));
        var controller = CreateController(workspace);
        var file = new FormFile(stream, 0, stream.Length, "file", "yeni.xml");

        var result = await controller.UploadAssets(file, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var storedFile = Assert.IsType<ReportDataFileSnapshot>(workspace.Get().AssetValues);
        Assert.Equal("yeni.xml", storedFile.FileName);
        var storedValue = Assert.Single(storedFile.Values);
        Assert.Equal(new DateOnly(2022, 1, 1), storedValue.Month);
        Assert.Equal(2_000m, storedValue.Value);
    }

    private static ImportsController CreateController(
        IReportWorkspaceSession? workspace = null) =>
        new(
            new ReadAssetValuesService(
                [new ClosedXmlAssetFileParser(), new XmlAssetFileParser()]),
            new ReadProducerPriceIndicesService(
                [new ClosedXmlProducerPriceIndexFileParser(), new XmlProducerPriceIndexFileParser()]),
            workspace ?? new TestReportWorkspaceSession());
}
