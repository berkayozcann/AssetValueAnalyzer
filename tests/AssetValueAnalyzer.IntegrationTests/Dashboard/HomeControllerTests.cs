using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.IntegrationTests.Dashboard;

public sealed class HomeControllerTests
{
    [Fact]
    public void Index_WithEmptySession_ReturnsDashboardWithoutReadyFiles()
    {
        var controller = new HomeController(new TestReportWorkspaceSession());

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Null(model.AssetValues);
        Assert.Null(model.ProducerPriceIndices);
    }

    [Fact]
    public void Index_WithValidatedFiles_RehydratesDashboardFileState()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_050_000m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xml",
            [
                new MonthlyProducerPriceIndexInput(new DateOnly(2021, 12, 1), 1_022.25m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 1_129.03m)
            ]);
        var controller = new HomeController(workspace);

        var result = Assert.IsType<ViewResult>(controller.Index());
        var model = Assert.IsType<DashboardPageViewModel>(result.Model);

        Assert.Equal("varlik.xlsx", model.AssetValues?.FileName);
        Assert.Equal(2, model.AssetValues?.ParsedCount);
        Assert.Equal(new DateOnly(2021, 12, 1), model.AssetValues?.FirstMonth);
        Assert.Equal(new DateOnly(2022, 1, 1), model.AssetValues?.LastMonth);
        Assert.Equal("endeks.xml", model.ProducerPriceIndices?.FileName);
        Assert.Equal(2, model.ProducerPriceIndices?.ParsedCount);
    }
}
