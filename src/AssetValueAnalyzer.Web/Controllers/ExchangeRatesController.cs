using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using AssetValueAnalyzer.Web.Features.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Web.Controllers;

[Route("exchange-rates")]
public sealed class ExchangeRatesController(
    ICurrentUsdExchangeRateReader currentRateReader,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("card")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Card(
        CancellationToken cancellationToken = default)
    {
        var exchangeRate = ExchangeRateCardViewModelFactory.Create(
            await currentRateReader.ReadAsync(cancellationToken),
            timeProvider);

        return PartialView("~/Views/Shared/_ExchangeRateCard.cshtml", exchangeRate);
    }
}
