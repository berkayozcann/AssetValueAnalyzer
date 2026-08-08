using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("reports")]
public sealed class ReportsController(
    IReportWorkspaceSession reportWorkspaceSession) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var snapshot = reportWorkspaceSession.Get();

        return snapshot.CompletedReport is not null
            ? View("Sample", snapshot.CompletedReport)
            : View(ReportWorkspacePageViewModel.FromSnapshot(snapshot));
    }

    [HttpGet("example")]
    public IActionResult Example()
    {
        return View("Sample", ReportPageViewModel.CreateSample());
    }

    [HttpGet("sample")]
    public IActionResult LegacySample()
    {
        return RedirectToActionPermanent(nameof(Example));
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        reportWorkspaceSession.Clear();
        return RedirectToAction("Index", "Home");
    }
}
