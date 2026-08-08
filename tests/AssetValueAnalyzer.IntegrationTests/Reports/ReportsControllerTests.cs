using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.IntegrationTests.Reports;

public sealed class ReportsControllerTests
{
    [Fact]
    public void Index_WithEmptySession_ReturnsEmptyWorkspace()
    {
        var controller = new ReportsController(new TestReportWorkspaceSession());

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<ReportWorkspacePageViewModel>(result.Model);

        Assert.Equal(ReportWorkspaceStatus.Empty, model.Status);
    }

    [Fact]
    public void Index_WithValidatedFile_ReturnsDraftWorkspace()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000_000m)]);
        var controller = new ReportsController(workspace);

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<ReportWorkspacePageViewModel>(result.Model);

        Assert.Equal(ReportWorkspaceStatus.Draft, model.Status);
        Assert.Equal("varlik.xlsx", model.AssetValues?.FileName);
        Assert.Null(model.ProducerPriceIndices);
    }

    [Fact]
    public void Example_ReturnsClearlyMarkedSampleReport()
    {
        var controller = new ReportsController(new TestReportWorkspaceSession());

        var result = Assert.IsType<ViewResult>(controller.Example());
        var model = Assert.IsType<ReportPageViewModel>(result.Model);

        Assert.Equal("Sample", result.ViewName);
        Assert.True(model.IsSample);
    }

    [Fact]
    public void Index_WithCompletedReport_ReturnsResultView()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveCompletedReport(ReportPageViewModel.CreateSample() with { IsSample = false });
        var controller = new ReportsController(workspace);

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<ReportPageViewModel>(result.Model);

        Assert.Equal("Sample", result.ViewName);
        Assert.False(model.IsSample);
    }
}
