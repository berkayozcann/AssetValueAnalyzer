using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Dashboard;
using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.IntegrationTests.Dashboard;

public sealed class HomeControllerTests
{
    [Fact]
    public async Task Index_WithEmptySession_ReturnsDashboardWithoutReadyFiles()
    {
        var controller = new HomeController(
            new TestReportWorkspaceSession(),
            new FakeCurrentUsdExchangeRateReader());

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Null(model.AssetValues);
        Assert.Null(model.ProducerPriceIndices);
    }

    [Fact]
    public async Task Index_WithValidatedFiles_RehydratesDashboardFileState()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_050_000m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xlsx",
            [
                new MonthlyProducerPriceIndexInput(new DateOnly(2021, 12, 1), 1_022.25m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 1_129.03m)
            ]);
        var controller = new HomeController(
            workspace,
            new FakeCurrentUsdExchangeRateReader());

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Equal("varlik.xlsx", model.AssetValues?.FileName);
        Assert.Equal(2, model.AssetValues?.ParsedCount);
        Assert.Equal(new DateOnly(2021, 12, 1), model.AssetValues?.FirstMonth);
        Assert.Equal(new DateOnly(2022, 1, 1), model.AssetValues?.LastMonth);
        Assert.Equal("Aralık 2021 – Ocak 2022", model.AssetValues?.MonthRange);
        Assert.Equal("endeks.xlsx", model.ProducerPriceIndices?.FileName);
        Assert.Equal(2, model.ProducerPriceIndices?.ParsedCount);
        Assert.Equal("Aralık 2021 – Ocak 2022", model.ProducerPriceIndices?.MonthRange);
    }

    [Fact]
    public async Task Index_WithCompletedReport_ExposesCompletedReportState()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveCompletedReport(TestReportPageViewModelFactory.Create());
        var controller = new HomeController(
            workspace,
            new FakeCurrentUsdExchangeRateReader());

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.True(model.HasCompletedReport);
    }
}
