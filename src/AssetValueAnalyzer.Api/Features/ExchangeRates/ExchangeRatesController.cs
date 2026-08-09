using AssetValueAnalyzer.Application.ExchangeRates.Queries;
using Microsoft.AspNetCore.Mvc;

namespace AssetValueAnalyzer.Api.Features.ExchangeRates;

[ApiController]
[Route("api/exchange-rates")]
public sealed class ExchangeRatesController(
    IExchangeRateReader exchangeRateReader) : ControllerBase
{
    [HttpGet("latest")]
    [ProducesResponseType<IReadOnlyList<ExchangeRateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateResponse>>> GetLatest(
        [FromQuery] GetLatestExchangeRatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var rates = await exchangeRateReader.ReadLatestAsync(
            new LatestExchangeRateQuery(
                query.RateDate,
                query.BaseCurrencyCode,
                query.ForeignCurrencyCode,
                query.Limit),
            cancellationToken);

        if (rates.Count == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Exchange rates not found",
                detail: "No exchange rates matched the requested date and currency filters.");
        }

        return Ok(rates.Select(ExchangeRateResponse.FromReadModel).ToArray());
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ExchangeRateResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateResponse>>> GetRange(
        [FromQuery] GetExchangeRatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var rates = await exchangeRateReader.ReadRangeAsync(
            new ExchangeRateRangeQuery(
                query.StartDate!.Value,
                query.EndDate!.Value,
                query.BaseCurrencyCode,
                query.ForeignCurrencyCode,
                query.Limit),
            cancellationToken);

        if (rates.Count == 0)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Exchange rates not found",
                detail: "No exchange rates matched the requested date range and currency filters.");
        }

        return Ok(rates.Select(ExchangeRateResponse.FromReadModel).ToArray());
    }
}
