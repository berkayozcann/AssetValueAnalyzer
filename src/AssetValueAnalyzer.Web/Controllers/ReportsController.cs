using System.Globalization;
using AssetValueAnalyzer.Application.Assets.Imports;
using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Application.ProducerPriceIndices.Imports;
using AssetValueAnalyzer.Application.Reports.Creation;
using AssetValueAnalyzer.Web.Features.Reports;
using AssetValueAnalyzer.Web.Features.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("reports")]
public sealed class ReportsController(
    IReportWorkspaceSession reportWorkspaceSession,
    CreateFinancialImpactReportService createReportService,
    FinancialImpactReportRangeValidator rangeValidator,
    ICurrentUsdExchangeRateReader currentRateReader) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var snapshot = reportWorkspaceSession.Get();
        var exchangeRate = await GetCurrentExchangeRateCardAsync(cancellationToken);

        return snapshot.CompletedReport is not null
            ? View("Result", snapshot.CompletedReport with { ExchangeRate = exchangeRate })
            : View(ReportWorkspacePageViewModel.FromSnapshot(snapshot, exchangeRate));
    }

    [HttpPost("validate-range")]
    [ValidateAntiForgeryToken]
    public IActionResult ValidateRange(CreateReportForm form)
    {
        var snapshot = reportWorkspaceSession.Get();

        if (snapshot.AssetValues is null || snapshot.ProducerPriceIndices is null)
        {
            return Ok(new
            {
                isValid = false,
                errors = new[]
                {
                    new
                    {
                        code = "MissingFiles",
                        message = "Tarih aralığını kontrol etmek için önce Varlık ve Endeks dosyalarını yükleyin."
                    }
                }
            });
        }

        if (!TryParseMonth(form.StartMonth, out var startMonth) ||
            !TryParseMonth(form.EndMonth, out var endMonth))
        {
            return Ok(new
            {
                isValid = false,
                errors = new[]
                {
                    new
                    {
                        code = "InvalidMonth",
                        message = "Tarih alanları geçerli bir ay ve yıl içermelidir."
                    }
                }
            });
        }

        var result = rangeValidator.Validate(CreateRequest(
            snapshot,
            startMonth,
            endMonth));

        return Ok(new
        {
            isValid = result.IsValid,
            effectiveStartMonth = result.EffectiveStartMonth?.ToString("yyyy-MM"),
            effectiveEndMonth = result.EffectiveEndMonth?.ToString("yyyy-MM"),
            includedMonthCount = result.SelectedAssetValues.Count,
            errors = result.Errors.Select(error => new
            {
                error.Code,
                error.Message,
                month = error.Month?.ToString("yyyy-MM")
            })
        });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateReportForm form,
        CancellationToken cancellationToken)
    {
        var snapshot = reportWorkspaceSession.Get();

        if (snapshot.AssetValues is null || snapshot.ProducerPriceIndices is null)
        {
            SetReportError("Rapor oluşturmak için önce Varlık ve Endeks dosyalarını yükleyin.");
            return RedirectAfterCreationError(snapshot);
        }

        if (!TryParseMonth(form.StartMonth, out var startMonth) ||
            !TryParseMonth(form.EndMonth, out var endMonth))
        {
            SetReportError("Tarih alanları geçerli bir ay ve yıl içermelidir.");
            return RedirectAfterCreationError(snapshot);
        }

        var result = await createReportService.CreateAsync(
            CreateRequest(snapshot, startMonth, endMonth),
            cancellationToken);

        if (!result.IsValid)
        {
            SetReportError(string.Join(" ", result.Errors.Select(error => error.Message)));
            return RedirectAfterCreationError(snapshot);
        }

        var reportViewModel = ReportPageViewModelFactory.Create(
            result.Report!,
            await GetCurrentExchangeRateCardAsync(cancellationToken),
            snapshot.AssetValues.FirstMonth,
            snapshot.AssetValues.LastMonth);
        reportWorkspaceSession.SaveCompletedReport(reportViewModel);
        TempData["ResetReportWizard"] = true;

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public IActionResult New()
    {
        reportWorkspaceSession.Clear();
        return RedirectToAction("Index", "Home");
    }

    private void SetReportError(string message)
    {
        TempData["ReportCreationError"] = message;
    }

    private IActionResult RedirectAfterCreationError(
        ReportWorkspaceSnapshot snapshot) =>
        snapshot.CompletedReport is null
            ? RedirectToAction("Index", "Home")
            : RedirectToAction(nameof(Index));

    private async Task<ExchangeRateCardViewModel> GetCurrentExchangeRateCardAsync(
        CancellationToken cancellationToken) =>
        ExchangeRateCardViewModelFactory.Create(
            await currentRateReader.ReadAsync(cancellationToken));

    private static CreateFinancialImpactReportRequest CreateRequest(
        ReportWorkspaceSnapshot snapshot,
        DateOnly? startMonth,
        DateOnly? endMonth) =>
        new(
            snapshot.AssetValues!.Values
                .Select(value => new MonthlyAssetValueInput(value.Month, value.Value))
                .ToArray(),
            snapshot.ProducerPriceIndices!.Values
                .Select(value => new MonthlyProducerPriceIndexInput(value.Month, value.Value))
                .ToArray(),
            startMonth,
            endMonth);

    private static bool TryParseMonth(
        string? value,
        out DateOnly? month)
    {
        month = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!DateOnly.TryParseExact(
                $"{value}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedMonth))
        {
            return false;
        }

        month = parsedMonth;
        return true;
    }
}
