using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AssetValueAnalyzer.Web.Features.Dashboard;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Web.Features.Shared;
using AssetValueAnalyzer.Web.Models;

namespace AssetValueAnalyzer.Web.Controllers;

public class HomeController(
    IReportWorkspaceSession reportWorkspaceSession,
    ICurrentUsdExchangeRateReader currentRateReader,
    TimeProvider timeProvider) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var exchangeRate = ExchangeRateCardViewModelFactory.Create(
            await currentRateReader.ReadAsync(cancellationToken),
            timeProvider);

        return View(DashboardPageViewModel.FromSnapshot(
            reportWorkspaceSession.Get(),
            exchangeRate));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
