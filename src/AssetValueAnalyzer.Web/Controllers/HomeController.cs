using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AssetValueAnalyzer.Web.Features.Dashboard;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.Web.Features.Shared;
using AssetValueAnalyzer.Web.Models;

namespace AssetValueAnalyzer.Web.Controllers;

public class HomeController(
    IReportWorkspaceSession reportWorkspaceSession,
    ICurrentUsdExchangeRateReader currentRateReader,
    FinancialImpactReportRangeValidator rangeValidator,
    TimeProvider timeProvider) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var snapshot = reportWorkspaceSession.Get();
        var exchangeRate = ExchangeRateCardViewModelFactory.Create(
            await currentRateReader.ReadAsync(cancellationToken),
            timeProvider);

        return View(DashboardPageViewModel.FromSnapshot(
            snapshot,
            exchangeRate,
            ValidateRestoredRange(snapshot)));
    }

    private DashboardRangeValidationViewModel ValidateRestoredRange(
        ReportWorkspaceSnapshot snapshot)
    {
        if (snapshot.AssetValues is null ||
            snapshot.ProducerPriceIndices is null ||
            snapshot.CompletedReport is not null)
        {
            return DashboardRangeValidationViewModel.Idle;
        }

        var request = new CreateFinancialImpactReportRequest(
            snapshot.AssetValues.Values
                .Select(value => new MonthlyAssetValueInput(value.Month, value.Value))
                .ToArray(),
            snapshot.ProducerPriceIndices.Values
                .Select(value => new MonthlyProducerPriceIndexInput(value.Month, value.Value))
                .ToArray(),
            snapshot.WizardState.StartMonth,
            snapshot.WizardState.EndMonth);

        return DashboardRangeValidationViewModel.FromResult(
            rangeValidator.Validate(request));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
