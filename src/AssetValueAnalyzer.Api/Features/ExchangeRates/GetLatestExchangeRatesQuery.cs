using System.ComponentModel.DataAnnotations;

namespace AssetValueAnalyzer.Api.Features.ExchangeRates;

public sealed class GetLatestExchangeRatesQuery
{
    public DateOnly? RateDate { get; init; }

    [Range(0, int.MaxValue)]
    public int? BaseCurrencyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? ForeignCurrencyCode { get; init; }

    [Range(1, 200)]
    public int Limit { get; init; } = 100;
}
