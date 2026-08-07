using AssetValueAnalyzer.Web.Features.Reports;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("reports")]
public sealed class ReportsController : Controller
{
    [HttpGet("")]
    [HttpGet("sample")]
    public IActionResult Sample()
    {
        return View(ReportPageViewModel.CreateSample());
    }
}
