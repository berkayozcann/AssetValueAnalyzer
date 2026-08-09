using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Infrastructure.Imports.Assets;
using AssetValueAnalyzer.Infrastructure.Imports.ProducerPriceIndices;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Reports;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.IntegrationTests.Assets;

public sealed class AssetUploadControllerTests
{
    [Fact]
    public async Task UploadAssets_WithValidXlsx_ReturnsValidationSummary()
    {
        await using var stream = CreateAssetWorkbook(
            (new DateTime(2021, 12, 1), 1_000_000m),
            (new DateTime(2022, 1, 1), 1_050_000m));
        var workspace = new TestReportWorkspaceSession();
        var controller = CreateController(workspace);
        var file = new FormFile(stream, 0, stream.Length, "file", "serbest-ad.xlsx");

        var result = await controller.UploadAssets(file, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AssetFileValidationResult>(ok.Value);
        Assert.True(response.IsValid);
        Assert.Equal(2, response.ParsedCount);
        Assert.Equal(new DateOnly(2021, 12, 1), response.FirstMonth);
        Assert.Equal(new DateOnly(2022, 1, 1), response.LastMonth);
        var storedFile = Assert.IsType<ReportDataFileSnapshot>(workspace.Get().AssetValues);
        Assert.Equal("serbest-ad.xlsx", storedFile.FileName);
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
        await using var stream = new MemoryStream([1]);
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveCompletedReport(TestReportPageViewModelFactory.Create());
        var controller = CreateController(workspace);
        var file = new FormFile(stream, 0, stream.Length, "file", "yeni.xlsx");

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
        await using var stream = CreateAssetWorkbook(
            (new DateTime(2022, 1, 1), 2_000m));
        var controller = CreateController(workspace);
        var file = new FormFile(stream, 0, stream.Length, "file", "yeni.xlsx");

        var result = await controller.UploadAssets(file, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var storedFile = Assert.IsType<ReportDataFileSnapshot>(workspace.Get().AssetValues);
        Assert.Equal("yeni.xlsx", storedFile.FileName);
        var storedValue = Assert.Single(storedFile.Values);
        Assert.Equal(new DateOnly(2022, 1, 1), storedValue.Month);
        Assert.Equal(2_000m, storedValue.Value);
    }

    private static ImportsController CreateController(
        IReportWorkspaceSession? workspace = null) =>
        new(
            new ReadAssetValuesService([new XlsxAssetFileParser()]),
            new ReadProducerPriceIndicesService(
                [new XlsxProducerPriceIndexFileParser()]),
            workspace ?? new TestReportWorkspaceSession());

    private static MemoryStream CreateAssetWorkbook(
        params (DateTime Month, decimal Amount)[] values)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Varlık Tablosu");
        worksheet.Cell(1, 1).Value = "Tarih";
        worksheet.Cell(1, 2).Value = "Varlık Tutarı";

        for (var index = 0; index < values.Length; index++)
        {
            var rowNumber = index + 2;
            worksheet.Cell(rowNumber, 1).Value = values[index].Month;
            worksheet.Cell(rowNumber, 1).Style.DateFormat.Format = "mmmm yyyy";
            worksheet.Cell(rowNumber, 2).Value = values[index].Amount;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }
}
