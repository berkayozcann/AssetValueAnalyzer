using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.IntegrationTests.Support;
using AssetValueAnalyzer.Web.Controllers;
using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc;

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
        Assert.Equal("Güncel USD/TRY", report.ExchangeRate.Label);
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

    private static ReportsController CreateController(
        TestReportWorkspaceSession workspace,
        IReadOnlyList<UsdCashChangeRate>? rates = null)
    {
        var rateReader = new FakeUsdCashChangeRateReader(rates ?? []);
        var service = new CreateFinancialImpactReportService(
            rateReader,
            new FinancialImpactReportRangeValidator(),
            new FinancialImpactCalculator());

        return new ReportsController(
            workspace,
            service,
            new FinancialImpactReportRangeValidator(),
            new FakeCurrentUsdExchangeRateReader(
                new CurrentUsdExchangeRate(
                    47.25m,
                    new DateOnly(2026, 8, 8),
                    new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero),
                    47m)));
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
}
