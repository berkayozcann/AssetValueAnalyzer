using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AssetValueAnalyzer.Web.Features.Dashboard;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Web.Models;

namespace AssetValueAnalyzer.Web.Controllers;

public class HomeController(
    IReportWorkspaceSession reportWorkspaceSession) : Controller
{
    public IActionResult Index()
    {
        return View(DashboardPageViewModel.FromSnapshot(reportWorkspaceSession.Get()));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
