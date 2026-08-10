using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AssetValueAnalyzer.IntegrationTests.Reports;

public sealed class ReportsControllerTests
{
    [Fact]
    public async Task Index_WithEmptySession_ReturnsEmptyWorkspace()
    {
        var controller = CreateController(new TestReportWorkspaceSession());

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<ReportWorkspacePageViewModel>(result.Model);

        Assert.Equal(ReportWorkspaceStatus.Empty, model.Status);
        Assert.Equal(0, model.ReadyFileCount);
        Assert.Equal(0, model.ReadyFileProgressPercent);
    }

    [Fact]
    public async Task Index_WithValidatedFile_ReturnsDraftWorkspace()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000_000m)]);
        var controller = CreateController(workspace);

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<ReportWorkspacePageViewModel>(result.Model);

        Assert.Equal(ReportWorkspaceStatus.Draft, model.Status);
        Assert.Equal("varlik.xlsx", model.AssetValues?.FileName);
        Assert.Equal("Aralık 2021 – Aralık 2021", model.AssetValues?.MonthRange);
        Assert.Null(model.ProducerPriceIndices);
        Assert.Equal(1, model.ReadyFileCount);
        Assert.Equal(50, model.ReadyFileProgressPercent);
    }

    [Fact]
    public async Task Index_WithBothValidatedFiles_ReturnsFullDraftProgress()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000_000m)]);
        workspace.SaveProducerPriceIndices(
            "yi-ufe.xlsx",
            [new MonthlyProducerPriceIndexInput(new DateOnly(2021, 12, 1), 1_022.25m)]);
        var controller = CreateController(workspace);

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<ReportWorkspacePageViewModel>(result.Model);

        Assert.Equal(ReportWorkspaceStatus.Draft, model.Status);
        Assert.True(model.HasBothFiles);
        Assert.Equal(2, model.ReadyFileCount);
        Assert.Equal(100, model.ReadyFileProgressPercent);
    }

    [Fact]
    public async Task Index_WithCompletedReport_ReturnsResultView()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveCompletedReport(TestReportPageViewModelFactory.Create());
        var controller = CreateController(workspace);

        var result = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<ReportPageViewModel>(result.Model);

        Assert.Equal("Result", result.ViewName);
        Assert.Equal("Aralık 2021 – Ocak 2022", model.Period);
    }

    [Fact]
    public async Task Create_WithValidatedWorkspace_CalculatesAndStoresRealReport()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_100m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xlsx",
            [
                new MonthlyProducerPriceIndexInput(new DateOnly(2021, 12, 1), 100m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 125m)
            ]);
        var controller = CreateController(
            workspace,
            [
                new UsdCashChangeRate(new DateOnly(2021, 12, 31), 10m),
                new UsdCashChangeRate(new DateOnly(2022, 1, 31), 20m)
            ]);

        var result = await controller.Create(
            new CreateReportForm(null, null),
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ReportsController.Index), redirect.ActionName);

        var report = Assert.IsType<ReportPageViewModel>(workspace.Get().CompletedReport);
        Assert.Equal("Aralık 2021 – Ocak 2022", report.Period);
        Assert.Equal(2, report.Rows.Count);
        Assert.Equal("₺1.100,00", report.Kpis[0].Value);
        Assert.Equal("+%10,00", report.Kpis[1].Value);
        Assert.Equal("47,2500", report.ExchangeRate.FormattedRate);
        Assert.Equal("USD / TRY", report.ExchangeRate.Label);
        Assert.True(report.ExchangeRate.HasRate);
        Assert.True(Assert.IsType<bool>(controller.TempData["ResetReportWizard"]));
    }

    [Fact]
    public void ValidateRange_WithMissingIndexMonth_ReturnsClearInvalidResult()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 2, 1), 1_100m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xlsx",
            [new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 100m)]);
        var controller = CreateController(workspace);

        var action = controller.ValidateRange(new CreateReportForm(null, null));
        var result = Assert.IsType<OkObjectResult>(action);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Value);
        using var json = System.Text.Json.JsonDocument.Parse(serialized);
        var message = json.RootElement
            .GetProperty("errors")[0]
            .GetProperty("Message")
            .GetString();

        Assert.Contains("MissingProducerPriceIndex", serialized);
        Assert.Contains("Şubat 2022", message);
        Assert.Contains("her ay", message);
    }

    [Fact]
    public void ValidateRange_AfterChoosingCoveredRange_ReturnsValidResult()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 2, 1), 1_100m),
                new MonthlyAssetValueInput(new DateOnly(2022, 3, 1), 1_200m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xlsx",
            [
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 100m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 2, 1), 110m)
            ]);
        var controller = CreateController(workspace);

        var action = controller.ValidateRange(new CreateReportForm("2022-01", "2022-02"));
        var result = Assert.IsType<OkObjectResult>(action);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"isValid\":true", serialized);
        Assert.Contains("\"includedMonthCount\":2", serialized);
    }

    [Fact]
    public async Task Create_FromCompletedReport_RecalculatesSelectedRangeAndKeepsFileBounds()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2021, 12, 1), 1_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_100m),
                new MonthlyAssetValueInput(new DateOnly(2022, 2, 1), 1_200m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xlsx",
            [
                new MonthlyProducerPriceIndexInput(new DateOnly(2021, 12, 1), 100m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 110m),
                new MonthlyProducerPriceIndexInput(new DateOnly(2022, 2, 1), 120m)
            ]);
        workspace.SaveCompletedReport(TestReportPageViewModelFactory.Create());
        var controller = CreateController(
            workspace,
            [
                new UsdCashChangeRate(new DateOnly(2022, 1, 31), 15m),
                new UsdCashChangeRate(new DateOnly(2022, 2, 28), 16m)
            ]);

        var result = await controller.Create(
            new CreateReportForm("2022-01", "2022-02"),
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ReportsController.Index), redirect.ActionName);
        var report = Assert.IsType<ReportPageViewModel>(workspace.Get().CompletedReport);
        Assert.Equal("Ocak 2022 – Şubat 2022", report.Period);
        Assert.Equal(new DateOnly(2022, 1, 1), report.StartMonth);
        Assert.Equal(new DateOnly(2022, 2, 1), report.EndMonth);
        Assert.Equal(new DateOnly(2021, 12, 1), report.AvailableStartMonth);
        Assert.Equal(new DateOnly(2022, 2, 1), report.AvailableEndMonth);
        Assert.Equal(2, report.Rows.Count);
    }

    [Fact]
    public async Task Create_FromCompletedReportWithInvalidRange_PreservesPreviousReport()
    {
        var workspace = new TestReportWorkspaceSession();
        workspace.SaveAssetValues(
            "varlik.xlsx",
            [
                new MonthlyAssetValueInput(new DateOnly(2022, 1, 1), 1_000m),
                new MonthlyAssetValueInput(new DateOnly(2022, 2, 1), 1_100m)
            ]);
        workspace.SaveProducerPriceIndices(
            "endeks.xlsx",
            [new MonthlyProducerPriceIndexInput(new DateOnly(2022, 1, 1), 100m)]);
        var previousReport = TestReportPageViewModelFactory.Create();
        workspace.SaveCompletedReport(previousReport);
        var controller = CreateController(workspace);

        var result = await controller.Create(
            new CreateReportForm("2022-01", "2022-02"),
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ReportsController.Index), redirect.ActionName);
        Assert.Same(previousReport, workspace.Get().CompletedReport);
    }

    private static ReportsController CreateController(
        TestReportWorkspaceSession workspace,
        IReadOnlyList<UsdCashChangeRate>? rates = null)
    {
        var rateReader = new FakeUsdCashChangeRateReader(rates ?? []);
        var service = new CreateFinancialImpactReportService(
            rateReader,
            new FinancialImpactReportRangeValidator(TimeProvider.System),
            new FinancialImpactCalculator());

        var controller = new ReportsController(
            workspace,
            service,
            new FinancialImpactReportRangeValidator(TimeProvider.System),
            new FakeCurrentUsdExchangeRateReader(
                new CurrentUsdExchangeRate(
                    47.25m,
                    new DateOnly(2026, 8, 8),
                    new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero),
                    47m)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.TempData = new TempDataDictionary(
            controller.HttpContext,
            new InMemoryTempDataProvider());

        return controller;
    }

    private sealed class FakeUsdCashChangeRateReader(
        IReadOnlyList<UsdCashChangeRate> rates) : IUsdCashChangeRateReader
    {
        public Task<IReadOnlyList<UsdCashChangeRate>> ReadAsync(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UsdCashChangeRate> result = rates
                .Where(rate => rate.RateDate >= startDate && rate.RateDate <= endDate)
                .ToArray();

            return Task.FromResult(result);
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private IDictionary<string, object> values = new Dictionary<string, object>();

        public IDictionary<string, object> LoadTempData(HttpContext context) => values;

        public void SaveTempData(
            HttpContext context,
            IDictionary<string, object> values) =>
            this.values = new Dictionary<string, object>(values);
    }
}
