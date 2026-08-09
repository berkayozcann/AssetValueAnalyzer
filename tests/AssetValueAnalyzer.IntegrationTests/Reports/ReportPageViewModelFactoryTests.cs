using AssetValueAnalyzer.Application.Reports.Calculation;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Web.Features.Shared;

namespace AssetValueAnalyzer.IntegrationTests.Reports;

public sealed class ReportPageViewModelFactoryTests
{
    [Fact]
    public void Create_FormatsCalculatedValuesForTurkishReportView()
    {
        var report = new FinancialImpactReport(
            new FinancialImpactReportSummary(
                new DateOnly(2022, 1, 1),
                1_100.50m,
                0.1005m,
                -0.125m,
                0m),
            [
                new FinancialImpactReportRow(
                    new DateOnly(2021, 12, 1),
                    1_000m,
                    0m,
                    0.1005m,
                    10m,
                    2_000m,
                    0m,
                    -0.125m,
                    -0.5m,
                    100m,
                    1_250m,
                    0m,
                    0m,
                    -0.2m),
                new FinancialImpactReportRow(
                    new DateOnly(2022, 1, 1),
                    1_100.50m,
                    0.1005m,
                    0m,
                    20m,
                    1_100.50m,
                    -0.44975m,
                    0m,
                    0m,
                    125m,
                    1_100.50m,
                    -0.1196m,
                    0m,
                    0m)
            ]);

        var currentRate = new ExchangeRateCardViewModel(
            "47,5000",
            "Artış",
            "Son senkronizasyon: 08.08.2026 12:00",
            ExchangeRateTrend.Increased);
        var viewModel = ReportPageViewModelFactory.Create(
            report,
            currentRate,
            new DateOnly(2021, 11, 1),
            new DateOnly(2022, 3, 1));

        Assert.Equal("Aralık 2021 – Ocak 2022", viewModel.Period);
        Assert.Equal("₺1.100,50", viewModel.Kpis[0].Value);
        Assert.Equal("+%10,05", viewModel.Kpis[1].Value);
        Assert.Equal("-%12,50", viewModel.Kpis[2].Value);
        Assert.Equal("%0,00", viewModel.Kpis[3].Value);
        Assert.Equal("—", viewModel.Rows[0].MonthlyAssetIncreaseRate);
        Assert.Equal("+%10,05", viewModel.Rows[1].MonthlyAssetIncreaseRate);
        Assert.Equal("47,5000", viewModel.ExchangeRate.FormattedRate);
        Assert.Equal(ExchangeRateTrend.Increased, viewModel.ExchangeRate.Trend);
        Assert.Equal(new DateOnly(2021, 12, 1), viewModel.StartMonth);
        Assert.Equal(new DateOnly(2022, 1, 1), viewModel.EndMonth);
        Assert.Equal(new DateOnly(2021, 11, 1), viewModel.AvailableStartMonth);
        Assert.Equal(new DateOnly(2022, 3, 1), viewModel.AvailableEndMonth);
    }

    [Fact]
    public void Create_FormatsMissingPreviousCalendarMonthChangesAsDash()
    {
        var report = new FinancialImpactReport(
            new FinancialImpactReportSummary(
                new DateOnly(2023, 3, 1),
                2_000_000m,
                0m,
                0m,
                0m),
            [
                new FinancialImpactReportRow(
                    new DateOnly(2023, 1, 1),
                    1_000_000m,
                    0m,
                    1m,
                    10m,
                    2_000_000m,
                    0m,
                    0m,
                    -0.5m,
                    100m,
                    1_250_000m,
                    0m,
                    0.6m,
                    -0.2m),
                new FinancialImpactReportRow(
                    new DateOnly(2023, 3, 1),
                    2_000_000m,
                    null,
                    0m,
                    20m,
                    2_000_000m,
                    null,
                    0m,
                    0m,
                    125m,
                    2_000_000m,
                    null,
                    0m,
                    0m)
            ]);

        var viewModel = ReportPageViewModelFactory.Create(
            report,
            new ExchangeRateCardViewModel(
                "47,5000",
                "Değişmedi",
                "Son senkronizasyon: 08.08.2026 12:00",
                ExchangeRateTrend.Unchanged),
            new DateOnly(2023, 1, 1),
            new DateOnly(2023, 3, 1));

        var marchRow = viewModel.Rows[1];
        Assert.Equal("—", marchRow.MonthlyAssetIncreaseRate);
        Assert.Equal("—", marchRow.MonthlyDollarizedIncreaseRate);
        Assert.Equal("—", marchRow.MonthlyInflationAdjustedIncreaseRate);
    }
}
